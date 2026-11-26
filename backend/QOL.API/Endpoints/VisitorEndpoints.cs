using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class VisitorEndpoints
{
    public const string VisitorCookieName = "QOL.Visitor";

    public static void MapVisitorEndpoints(this WebApplication app)
    {
        app.MapPost("/api/access/start", async Task<IResult> (
            StartVisitorSessionRequest request,
            HttpContext httpContext,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.DeviceId, out var deviceId) || deviceId == Guid.Empty)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.DeviceId)] = ["Device ID must be a non-empty UUID."]
                });
            }

            var now = DateTimeOffset.UtcNow;
            var session = await db.VisitorSessions
                .Include(item => item.Language)
                .SingleOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken);
            if (session is null)
            {
                session = new VisitorSession(deviceId, now);
                db.VisitorSessions.Add(session);
            }
            else
            {
                session.Touch(now);
            }

            await db.SaveChangesAsync(cancellationToken);
            httpContext.Response.Cookies.Append(
                VisitorCookieName,
                session.Id.ToString("D"),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Strict,
                    Secure = httpContext.Request.IsHttps,
                    Path = "/api",
                    Expires = session.ExpiresAt
                });

            return TypedResults.Ok(new VisitorSessionResponse(
                session.Id,
                session.Language?.Code,
                session.ExpiresAt));
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithTags("Visitor access")
        .WithName("StartVisitorSession")
        .WithSummary("Start a limited anonymous visitor session")
        .Produces<VisitorSessionResponse>()
        .ProducesValidationProblem();

        app.MapPut("/api/session/language", async Task<IResult> (
            SetVisitorLanguageRequest request,
            HttpContext httpContext,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetVisitorSessionId(httpContext, out var sessionId))
            {
                return TypedResults.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var session = await db.VisitorSessions.SingleOrDefaultAsync(
                item => item.Id == sessionId && item.ExpiresAt > now,
                cancellationToken);
            if (session is null)
            {
                httpContext.Response.Cookies.Delete(VisitorCookieName, new CookieOptions
                {
                    Path = "/api",
                    SameSite = SameSiteMode.Strict,
                    Secure = httpContext.Request.IsHttps
                });
                return TypedResults.Unauthorized();
            }

            var normalizedCode = request.LanguageCode.Trim().ToLowerInvariant();
            var language = await db.Languages.SingleOrDefaultAsync(
                item => item.Code == normalizedCode && item.IsEnabled,
                cancellationToken);
            if (language is null)
            {
                return TypedResults.NotFound();
            }

            session.SetLanguage(language.Id, now);
            await db.SaveChangesAsync(cancellationToken);
            httpContext.Response.Cookies.Append(
                VisitorCookieName,
                session.Id.ToString("D"),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Strict,
                    Secure = httpContext.Request.IsHttps,
                    Path = "/api",
                    Expires = session.ExpiresAt
                });

            return TypedResults.Ok(new VisitorLanguageResponse(language.Code, language.Name));
        })
        .AllowAnonymous()
        .RequireRateLimiting("sensitive-write")
        .WithTags("Visitor access")
        .WithName("SetVisitorLanguage")
        .WithSummary("Set the language preference for the current visitor session")
        .Produces<VisitorLanguageResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapGet("/api/policy", () => TypedResults.Ok(new SystemPolicyResponse(
            Version: "1.0",
            VisitorSessionLifetimeDays: 30,
            VisitorCookieHttpOnly: true,
            VisitorCookieSameSite: "Strict",
            VisitorCookiePath: "/api",
            VisitorSessionGrantsAudioAccess: false,
            AudioPlaybackRequiresActiveSubscription: true,
            AccountOperationsRequireAuthentication: true,
            ListeningHistoryRecordedOnAuthorizedPlayback: true)))
        .AllowAnonymous()
        .WithTags("Policy")
        .WithName("GetSystemPolicy")
        .WithSummary("Describe visitor-session and audio-access rules");
    }

    private static bool TryGetVisitorSessionId(HttpContext context, out Guid sessionId) =>
        Guid.TryParse(context.Request.Cookies[VisitorCookieName], out sessionId);
}

public sealed record StartVisitorSessionRequest
{
    [Required, StringLength(36, MinimumLength = 32)]
    public required string DeviceId { get; init; }
}

public sealed record SetVisitorLanguageRequest
{
    [Required, StringLength(10, MinimumLength = 2)]
    public required string LanguageCode { get; init; }
}

public sealed record VisitorSessionResponse(
    Guid SessionId,
    string? LanguageCode,
    DateTimeOffset ExpiresAt);

public sealed record VisitorLanguageResponse(string LanguageCode, string LanguageName);

public sealed record SystemPolicyResponse(
    string Version,
    int VisitorSessionLifetimeDays,
    bool VisitorCookieHttpOnly,
    string VisitorCookieSameSite,
    string VisitorCookiePath,
    bool VisitorSessionGrantsAudioAccess,
    bool AudioPlaybackRequiresActiveSubscription,
    bool AccountOperationsRequireAuthentication,
    bool ListeningHistoryRecordedOnAuthorizedPlayback);
