using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class CommerceEndpoints
{
    public static void MapCommerceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/plans", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plans = await db.SubscriptionPlans
                .AsNoTracking()
                .Where(plan => plan.IsActive)
                .OrderBy(plan => plan.Price)
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(plans.Select(ToPlanResponse).ToArray());
        })
        .WithTags("Subscription plans")
        .WithName("GetAvailablePlans")
        .WithSummary("List available subscription plans")
        .Produces<SubscriptionPlanResponse[]>();

        var adminPlans = app.MapGroup("/api/admin/plans")
            .WithTags("Administration", "Subscription plans")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminPlans.MapGet("/", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plans = await db.SubscriptionPlans
                .AsNoTracking()
                .OrderByDescending(plan => plan.CreatedAt)
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(plans.Select(ToPlanResponse).ToArray());
        })
        .WithName("GetAdminPlans")
        .Produces<SubscriptionPlanResponse[]>();

        adminPlans.MapPost("/", async Task<Created<SubscriptionPlanResponse>> (
            SubscriptionPlanRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plan = new SubscriptionPlan(
                request.Name,
                request.Description,
                request.Price,
                request.Currency,
                request.DurationDays);
            db.SubscriptionPlans.Add(plan);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/plans/{plan.Id}", ToPlanResponse(plan));
        })
        .WithName("CreateSubscriptionPlan")
        .Produces<SubscriptionPlanResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        adminPlans.MapPut("/{planId:guid}", async Task<IResult> (
            Guid planId,
            SubscriptionPlanRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(
                item => item.Id == planId,
                cancellationToken);
            if (plan is null)
            {
                return TypedResults.NotFound();
            }

            plan.UpdateDetails(
                request.Name,
                request.Description,
                request.Price,
                request.Currency,
                request.DurationDays);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(ToPlanResponse(plan));
        })
        .WithName("UpdateSubscriptionPlan")
        .Produces<SubscriptionPlanResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        adminPlans.MapPatch("/{planId:guid}/visibility", async Task<IResult> (
            Guid planId,
            UpdatePlanVisibilityRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(
                item => item.Id == planId,
                cancellationToken);
            if (plan is null)
            {
                return TypedResults.NotFound();
            }

            plan.SetActive(request.IsActive);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("SetSubscriptionPlanVisibility")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        var myOrders = app.MapGroup("/api/me/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        myOrders.MapGet("/", async Task<Results<Ok<SubscriptionOrderResponse[]>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var orders = await db.SubscriptionOrders
                .AsNoTracking()
                .Where(order => order.UserId == userId)
                .OrderByDescending(order => order.CreatedAt)
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(orders.Select(ToOrderResponse).ToArray());
        })
        .WithName("GetMySubscriptionOrders")
        .Produces<SubscriptionOrderResponse[]>();

        myOrders.MapPost("/", async Task<IResult> (
            CreateSubscriptionOrderRequest request,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(
                item => item.Id == request.PlanId && item.IsActive,
                cancellationToken);
            if (plan is null)
            {
                return TypedResults.NotFound();
            }

            var pendingDuplicate = await db.SubscriptionOrders.AnyAsync(
                order => order.UserId == userId &&
                    order.PlanId == plan.Id &&
                    order.Status == SubscriptionOrderStatus.Pending,
                cancellationToken);
            if (pendingDuplicate)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Pending order already exists",
                    Detail = "Wait for the existing order to be reviewed before creating another order for this plan."
                });
            }

            var order = new SubscriptionOrder(userId, plan);
            db.SubscriptionOrders.Add(order);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created(
                $"/api/me/orders/{order.Id}",
                ToOrderResponse(order));
        })
        .WithName("CreateSubscriptionOrder")
        .WithSummary("Create an order that remains pending until an administrator verifies payment")
        .Produces<SubscriptionOrderResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        var adminOrders = app.MapGroup("/api/admin/orders")
            .WithTags("Administration", "Orders")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminOrders.MapGet("/", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var orders = await db.SubscriptionOrders
                .AsNoTracking()
                .OrderBy(order => order.Status)
                .ThenByDescending(order => order.CreatedAt)
                .Select(order => new AdminSubscriptionOrderResponse(
                    order.Id,
                    order.User.Email,
                    order.PlanName,
                    order.Amount,
                    order.Currency,
                    order.Status.ToString(),
                    order.CreatedAt,
                    order.ReviewedAt,
                    order.ReviewNote))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(orders);
        })
        .WithName("GetAdminSubscriptionOrders")
        .Produces<AdminSubscriptionOrderResponse[]>();

        adminOrders.MapPatch("/{orderId:guid}/review", async Task<IResult> (
            Guid orderId,
            ReviewSubscriptionOrderRequest request,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var adminUserId))
            {
                return TypedResults.Unauthorized();
            }

            if (!Enum.TryParse<SubscriptionOrderStatus>(request.Status, ignoreCase: true, out var status) ||
                status == SubscriptionOrderStatus.Pending)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Status)] = ["Status must be Verified or Rejected."]
                });
            }

            var order = await db.SubscriptionOrders
                .Include(item => item.Plan)
                .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
            if (order is null)
            {
                return TypedResults.NotFound();
            }

            try
            {
                order.Review(status, adminUserId, request.Note);
                if (status == SubscriptionOrderStatus.Verified)
                {
                    var subscriptionExists = await db.UserSubscriptions.AnyAsync(
                        item => item.OrderId == order.Id,
                        cancellationToken);
                    if (!subscriptionExists)
                    {
                        db.UserSubscriptions.Add(new UserSubscription(
                            order.UserId,
                            order.PlanId,
                            order.Id,
                            order.PlanName,
                            order.Currency,
                            order.Amount,
                            order.Plan.DurationDays));
                    }
                }
            }
            catch (InvalidOperationException exception)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Order already reviewed",
                    Detail = exception.Message
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(ToOrderResponse(order));
        })
        .WithName("ReviewSubscriptionOrder")
        .WithSummary("Manually verify or reject a pending order")
        .Produces<SubscriptionOrderResponse>()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapGet("/api/me/subscriptions", async Task<IResult> (
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var subscriptions = await db.UserSubscriptions
                .AsNoTracking()
                .Where(subscription => subscription.UserId == userId)
                .OrderByDescending(subscription => subscription.StartsAt)
                .Select(subscription => new UserSubscriptionResponse(
                    subscription.Id,
                    subscription.PlanName,
                    subscription.StartsAt,
                    subscription.EndsAt,
                    subscription.IsActive && subscription.EndsAt > now))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(subscriptions);
        })
        .RequireAuthorization()
        .WithTags("Subscriptions")
        .WithName("GetMySubscriptions")
        .Produces<UserSubscriptionResponse[]>();

        app.MapGet("/api/admin/dashboard", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var counts = new AdminDashboardResponse(
                await db.Users.CountAsync(cancellationToken),
                await db.Locations.CountAsync(location => location.IsActive, cancellationToken),
                await db.AudioFiles.CountAsync(audio => audio.IsActive, cancellationToken),
                await db.SubscriptionPlans.CountAsync(plan => plan.IsActive, cancellationToken),
                await db.SubscriptionOrders.CountAsync(
                    order => order.Status == SubscriptionOrderStatus.Pending,
                    cancellationToken));
            return TypedResults.Ok(counts);
        })
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .WithTags("Administration")
        .WithName("GetAdminDashboard")
        .WithSummary("Get key QOL administration counts")
        .Produces<AdminDashboardResponse>();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static SubscriptionPlanResponse ToPlanResponse(SubscriptionPlan plan) =>
        new(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.Price,
            plan.Currency,
            plan.DurationDays,
            plan.IsActive);

    private static SubscriptionOrderResponse ToOrderResponse(SubscriptionOrder order) =>
        new(
            order.Id,
            order.PlanId,
            order.PlanName,
            order.Amount,
            order.Currency,
            order.Status.ToString(),
            order.CreatedAt,
            order.ReviewedAt,
            order.ReviewNote);
}

public sealed record SubscriptionPlanRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, StringLength(1000, MinimumLength = 1)]
    public required string Description { get; init; }

    [Range(typeof(decimal), "0", "1000000000")]
    public required decimal Price { get; init; }

    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public required string Currency { get; init; }

    [Range(1, 3650)]
    public required int DurationDays { get; init; }
}

public sealed record UpdatePlanVisibilityRequest
{
    public required bool IsActive { get; init; }
}

public sealed record CreateSubscriptionOrderRequest
{
    [Required]
    public required Guid PlanId { get; init; }
}

public sealed record ReviewSubscriptionOrderRequest
{
    [Required, RegularExpression("^(Verified|Rejected)$")]
    public required string Status { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }
}

public sealed record SubscriptionPlanResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int DurationDays,
    bool IsActive);

public sealed record SubscriptionOrderResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);

public sealed record AdminSubscriptionOrderResponse(
    Guid Id,
    string UserEmail,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);

public sealed record UserSubscriptionResponse(
    Guid Id,
    string PlanName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive);

public sealed record AdminDashboardResponse(
    int UserCount,
    int ActiveLocationCount,
    int ActiveAudioCount,
    int ActivePlanCount,
    int PendingOrderCount);
