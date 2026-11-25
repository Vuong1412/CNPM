using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QOL.Application.Accounts;
using QOL.Domain.Entities;

namespace QOL.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        auth.MapPost("/register", async Task<Results<Created<AuthUserResponse>, Conflict<ProblemDetails>, ValidationProblem>> (
            RegisterRequest request,
            IUserAccountService accounts,
            IPasswordHasher<User> passwordHasher,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!HasStrongPassword(request.Password))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Password)] =
                    [
                        "Password must be at least 12 characters and include uppercase, lowercase, a number, and a symbol."
                    ]
                });
            }

            var userForHash = new User(request.Email, request.DisplayName);
            var passwordHash = passwordHasher.HashPassword(userForHash, request.Password);
            var user = await accounts.RegisterAsync(
                request.Email,
                request.DisplayName,
                passwordHash,
                cancellationToken);

            if (user is null)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Email already registered",
                    Detail = "An account with that email already exists."
                });
            }

            var response = ToResponse(user);
            await SignInAsync(httpContext, user, isPersistent: false);
            return TypedResults.Created("/api/auth/me", response);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("RegisterUser")
        .WithSummary("Register a customer account")
        .WithDescription("Creates a user account and starts an authenticated session.")
        .Produces<AuthUserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        auth.MapPost("/login", async Task<Results<Ok<AuthUserResponse>, UnauthorizedHttpResult>> (
            LoginRequest request,
            IUserAccountService accounts,
            IPasswordHasher<User> passwordHasher,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var user = await accounts.FindByEmailAsync(request.Email, cancellationToken);
            if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
            {
                return TypedResults.Unauthorized();
            }

            var verification = passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return TypedResults.Unauthorized();
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var updatedHash = passwordHasher.HashPassword(user, request.Password);
                await accounts.UpdatePasswordHashAsync(user, updatedHash, cancellationToken);
            }

            await SignInAsync(httpContext, user, request.RememberMe);
            return TypedResults.Ok(ToResponse(user));
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("Login")
        .WithSummary("Sign in to a customer or administrator account")
        .WithDescription("Returns a secure HTTP-only authentication cookie.")
        .Produces<AuthUserResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        auth.MapPost("/bootstrap-admin", async Task<Results<
            Created<AuthUserResponse>,
            Conflict<ProblemDetails>,
            UnauthorizedHttpResult,
            ProblemHttpResult>> (
            BootstrapAdminRequest request,
            IConfiguration configuration,
            IUserAccountService accounts,
            IPasswordHasher<User> passwordHasher,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var expectedToken = configuration["Authentication:AdminBootstrapToken"];
            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Administrator bootstrap is disabled",
                    detail: "Configure Authentication:AdminBootstrapToken using .NET User Secrets before creating the first administrator.");
            }

            if (!FixedTimeEquals(request.BootstrapToken, expectedToken))
            {
                return TypedResults.Unauthorized();
            }

            if (!HasStrongPassword(request.Password))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Password does not meet the security requirements",
                    detail: "Password must be at least 12 characters and include uppercase, lowercase, a number, and a symbol.");
            }

            var userForHash = new User(request.Email, request.DisplayName);
            var passwordHash = passwordHasher.HashPassword(userForHash, request.Password);
            var result = await accounts.CreateFirstAdminAsync(
                request.Email,
                request.DisplayName,
                passwordHash,
                cancellationToken);

            if (result.Status == FirstAdminCreationStatus.AlreadyExists)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Administrator already exists",
                    Detail = "The first administrator has already been created."
                });
            }

            if (result.Status == FirstAdminCreationStatus.EmailAlreadyExists)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Email already registered",
                    Detail = "An account with that email already exists."
                });
            }

            var user = result.User!;
            var response = ToResponse(user);
            await SignInAsync(httpContext, user, isPersistent: false);
            return TypedResults.Created("/api/auth/me", response);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("BootstrapFirstAdmin")
        .WithSummary("Create the first administrator")
        .WithDescription("One-time setup guarded by a secret configured outside source control. It cannot create additional administrators.")
        .Produces<AuthUserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        auth.MapGet("/me", async Task<Results<Ok<AuthUserResponse>, UnauthorizedHttpResult, NotFound>> (
            ClaimsPrincipal principal,
            IUserAccountService accounts,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var user = await accounts.FindByIdAsync(userId, cancellationToken);
            return user is null || !user.IsActive
                ? TypedResults.NotFound()
                : TypedResults.Ok(ToResponse(user));
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithSummary("Get the signed-in user's profile")
        .Produces<AuthUserResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        auth.MapGet("/antiforgery", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return TypedResults.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
        })
        .AllowAnonymous()
        .WithName("GetAntiforgeryToken")
        .WithSummary("Issue a request token for browser uploads")
        .Produces<AntiforgeryTokenResponse>();

        auth.MapPatch("/me", async Task<Results<Ok<AuthUserResponse>, UnauthorizedHttpResult, NotFound>> (
            UpdateProfileRequest request,
            ClaimsPrincipal principal,
            IUserAccountService accounts,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var user = await accounts.UpdateProfileAsync(userId, request.DisplayName, cancellationToken);
            return user is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ToResponse(user));
        })
        .RequireAuthorization()
        .RequireRateLimiting("sensitive-write")
        .WithName("UpdateCurrentUserProfile")
        .WithSummary("Update the signed-in user's display name")
        .Produces<AuthUserResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        auth.MapPost("/me/password", async Task<Results<NoContent, UnauthorizedHttpResult, ValidationProblem, NotFound>> (
            ChangePasswordRequest request,
            ClaimsPrincipal principal,
            IUserAccountService accounts,
            IPasswordHasher<User> passwordHasher,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var user = await accounts.FindByIdAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return TypedResults.NotFound();
            }

            if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
                == PasswordVerificationResult.Failed)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.CurrentPassword)] = ["Current password is incorrect."]
                });
            }

            if (!HasStrongPassword(request.NewPassword))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.NewPassword)] =
                    [
                        "Password must be at least 12 characters and include uppercase, lowercase, a number, and a symbol."
                    ]
                });
            }

            var updatedHash = passwordHasher.HashPassword(user, request.NewPassword);
            await accounts.UpdatePasswordHashAsync(user, updatedHash, cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireAuthorization()
        .RequireRateLimiting("sensitive-write")
        .WithName("ChangeCurrentUserPassword")
        .WithSummary("Change the signed-in user's password")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        auth.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.NoContent();
        })
        .RequireAuthorization()
        .RequireRateLimiting("sensitive-write")
        .WithName("Logout")
        .WithSummary("Sign out of the current session")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static AuthUserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt);

    private static async Task SignInAsync(HttpContext httpContext, User user, bool isPersistent)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = isPersistent });
    }

    private static bool HasStrongPassword(string password) =>
        password.Length >= 12 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(character => !char.IsLetterOrDigit(character));

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}

/// <summary>Payload for creating a customer account.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(320)]
    public required string Email { get; init; }

    [Required, StringLength(150, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    [Required, StringLength(128, MinimumLength = 12)]
    public required string Password { get; init; }
}

/// <summary>Credentials for an existing account.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(320)]
    public required string Email { get; init; }

    [Required, StringLength(128)]
    public required string Password { get; init; }

    public bool RememberMe { get; init; }
}

/// <summary>Payload for the one-time creation of the first administrator.</summary>
public sealed record BootstrapAdminRequest
{
    [Required, EmailAddress, StringLength(320)]
    public required string Email { get; init; }

    [Required, StringLength(150, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    [Required, StringLength(128, MinimumLength = 12)]
    public required string Password { get; init; }

    [Required, StringLength(256, MinimumLength = 32)]
    public required string BootstrapToken { get; init; }
}

/// <summary>Payload for updating a user's display name.</summary>
public sealed record UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public required string DisplayName { get; init; }
}

public sealed record ChangePasswordRequest
{
    [Required, StringLength(128)]
    public required string CurrentPassword { get; init; }

    [Required, StringLength(128, MinimumLength = 12)]
    public required string NewPassword { get; init; }
}

/// <summary>Public profile and role information for an authenticated account.</summary>
public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record AntiforgeryTokenResponse(string RequestToken);
