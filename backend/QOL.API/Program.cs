using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QOL.API.Endpoints;
using QOL.API.HealthChecks;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection cannot be empty.");
}

builder.Services.AddDbContext<QolDbContext>(options => options.UseSqlServer(connectionString));
var audioDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "audio");
Directory.CreateDirectory(audioDirectory);
builder.Services.AddQolInfrastructure();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "QOL.AntiCsrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "QOL.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/api";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(audioDirectory),
    RequestPath = "/media/audio"
});

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        (context.Request.Method == "POST" ||
         context.Request.Method == "PUT" ||
         context.Request.Method == "PATCH" ||
         context.Request.Method == "DELETE"))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context);
    }

    await next();
});
app.MapHealthChecks("/health");
app.MapGet("/", () => TypedResults.Ok(new { name = "QOL API", status = "ready" }))
    .WithTags("System");

app.MapAuthEndpoints();
app.MapLocationEndpoints();
app.MapAudioEndpoints(audioDirectory);
app.MapUserActivityEndpoints();
app.MapCommerceEndpoints();
app.MapReviewEndpoints();

app.MapGet("/api/languages", async (QolDbContext db, CancellationToken cancellationToken) =>
    await db.Languages
        .AsNoTracking()
        .Where(language => language.IsEnabled)
        .OrderBy(language => language.Name)
        .Select(language => new LanguageResponse(
            language.Id,
            language.Code,
            language.Name,
            language.NativeName,
            language.IsEnabled))
        .ToListAsync(cancellationToken))
    .WithTags("Languages")
    .WithSummary("List enabled languages");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

/// <summary>Language options available for user-facing content.</summary>
public sealed record LanguageResponse(
    Guid Id,
    string Code,
    string Name,
    string NativeName,
    bool IsEnabled);
