using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QOL.Application.Commerce;
using QOL.Application.Payments;
using QOL.Application.Subscriptions;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class CommerceEndpoints
{
    private const string ManualQrPaymentProvider = "ManualQR";

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
            .RequireRateLimiting("sensitive-write")
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
            .RequireRateLimiting("sensitive-write")
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
                .Select(order => new
                {
                    Order = order,
                    ManualQrReportedAt = db.Payments
                        .Where(payment => payment.OrderId == order.Id &&
                            payment.Provider == ManualQrPaymentProvider)
                        .Select(payment => (DateTimeOffset?)payment.CreatedAt)
                        .FirstOrDefault()
                })
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(orders
                .Select(item => ToOrderResponse(item.Order, item.ManualQrReportedAt))
                .ToArray());
        })
        .WithName("GetMySubscriptionOrders")
        .Produces<SubscriptionOrderResponse[]>();

        myOrders.MapPost("/{orderId:guid}/qr-payment-claim", async Task<IResult> (
            Guid orderId,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var order = await db.SubscriptionOrders.SingleOrDefaultAsync(
                item => item.Id == orderId && item.UserId == userId,
                cancellationToken);
            if (order is null)
            {
                return TypedResults.NotFound();
            }

            if (order.Status != SubscriptionOrderStatus.Pending)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Order is not pending",
                    Detail = "Only pending orders can be reported as paid."
                });
            }

            var existingClaim = await db.Payments
                .AsNoTracking()
                .Where(payment => payment.OrderId == orderId &&
                    payment.Provider == ManualQrPaymentProvider)
                .Select(payment => (DateTimeOffset?)payment.CreatedAt)
                .SingleOrDefaultAsync(cancellationToken);
            if (existingClaim.HasValue)
            {
                return TypedResults.Ok(new QrPaymentClaimResponse(existingClaim.Value));
            }

            var claim = new Payment(
                order.Id,
                ManualQrPaymentProvider,
                $"QR-CLAIM-{order.Id:N}",
                order.Amount,
                order.Currency);
            db.Payments.Add(claim);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.Entry(claim).State = EntityState.Detached;
                var concurrentClaim = await db.Payments
                    .AsNoTracking()
                    .Where(payment => payment.OrderId == orderId &&
                        payment.Provider == ManualQrPaymentProvider)
                    .Select(payment => (DateTimeOffset?)payment.CreatedAt)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!concurrentClaim.HasValue)
                {
                    throw;
                }

                return TypedResults.Ok(new QrPaymentClaimResponse(concurrentClaim.Value));
            }

            return TypedResults.Ok(new QrPaymentClaimResponse(claim.CreatedAt));
        })
        .WithName("ReportQrPayment")
        .WithSummary("Record a user's manual QR payment claim without verifying the order")
        .Produces<QrPaymentClaimResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

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

        myOrders.MapPost("/{orderId:guid}/pay", async Task<IResult> (
            Guid orderId,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            IConfiguration configuration,
            QolDbContext db,
            IPaymentGateway paymentGateway,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var order = await db.SubscriptionOrders.SingleOrDefaultAsync(
                item => item.Id == orderId && item.UserId == userId,
                cancellationToken);
            if (order is null)
            {
                return TypedResults.NotFound();
            }

            if (order.Status != SubscriptionOrderStatus.Pending)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Order is not pending",
                    Detail = "Only pending orders can be paid."
                });
            }

            if (order.Amount <= 0 ||
                !string.Equals(order.Currency, "VND", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(order.Amount)] = ["Online VNPay payments require a positive amount in VND."]
                });
            }

            var transactionReference = Guid.NewGuid().ToString("N");
            var payment = new Payment(
                order.Id,
                paymentGateway.Provider,
                transactionReference,
                order.Amount,
                order.Currency);
            var returnUrl = configuration["VnPay:ReturnUrl"];
            var ipnUrl = configuration["VnPay:IpnUrl"];
            var origin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = $"{origin}/payment/result";
            }
            if (string.IsNullOrWhiteSpace(ipnUrl))
            {
                ipnUrl = $"{origin}/api/payments/vnpay/ipn";
            }

            returnUrl = AddQueryParameter(returnUrl, "orderId", order.Id.ToString("D"));
            string paymentUrl;
            try
            {
                paymentUrl = await paymentGateway.CreatePaymentUrlAsync(
                    new PaymentLinkRequest(
                        payment.Id,
                        order.Id,
                        payment.ProviderTxnId,
                        order.Amount,
                        order.Currency,
                        returnUrl,
                        ipnUrl,
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"),
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Online payment is unavailable",
                    detail: exception.Message);
            }

            db.Payments.Add(payment);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(new PaymentLinkResponse(payment.Id, paymentUrl));
        })
        .WithName("CreateOrderPaymentLink")
        .WithSummary("Create a VNPay payment link for the signed-in user's pending order")
        .Produces<PaymentLinkResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .ProducesValidationProblem();

        var adminOrders = app.MapGroup("/api/admin/orders")
            .WithTags("Administration", "Orders")
            .RequireRateLimiting("sensitive-write")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminOrders.MapGet("/", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var orders = await db.SubscriptionOrders
                .AsNoTracking()
                .OrderBy(order => order.Status)
                .ThenByDescending(order => order.CreatedAt)
                .Select(order => new
                {
                    Order = order,
                    ManualQrReportedAt = db.Payments
                        .Where(payment => payment.OrderId == order.Id &&
                            payment.Provider == ManualQrPaymentProvider)
                        .Select(payment => (DateTimeOffset?)payment.CreatedAt)
                        .FirstOrDefault()
                })
                .Select(item => new AdminSubscriptionOrderResponse(
                    item.Order.Id,
                    item.Order.User.Email,
                    item.Order.PlanName,
                    item.Order.Amount,
                    item.Order.Currency,
                    item.Order.Status.ToString(),
                    item.Order.CreatedAt,
                    item.Order.ReviewedAt,
                    item.Order.ReviewNote,
                    item.ManualQrReportedAt))
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
            IOrderFulfillmentService orderFulfillment,
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

            if (status == SubscriptionOrderStatus.Verified)
            {
                var fulfillment = await orderFulfillment.VerifyAsync(
                    orderId,
                    adminUserId,
                    request.Note,
                    cancellationToken);
                if (fulfillment == OrderFulfillmentResult.NotFound)
                {
                    return TypedResults.NotFound();
                }

                if (fulfillment == OrderFulfillmentResult.Rejected)
                {
                    return TypedResults.Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Order was rejected",
                        Detail = "A rejected order cannot be verified."
                    });
                }

                var verifiedOrder = await db.SubscriptionOrders
                    .AsNoTracking()
                    .SingleAsync(item => item.Id == orderId, cancellationToken);
                return TypedResults.Ok(ToOrderResponse(verifiedOrder));
            }

            var rejectedOrder = await db.SubscriptionOrders.SingleOrDefaultAsync(
                item => item.Id == orderId,
                cancellationToken);
            if (rejectedOrder is null)
            {
                return TypedResults.NotFound();
            }

            try
            {
                rejectedOrder.Review(status, adminUserId, request.Note);
                await db.SaveChangesAsync(cancellationToken);
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

            return TypedResults.Ok(ToOrderResponse(rejectedOrder));
        })
        .WithName("ReviewSubscriptionOrder")
        .WithSummary("Manually verify or reject a pending order")
        .Produces<SubscriptionOrderResponse>()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapGet("/api/me/subscriptions", async Task<IResult> (
            ClaimsPrincipal principal,
            ISubscriptionAccessService subscriptionAccess,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var userSubscriptions = await subscriptionAccess.GetUserSubscriptionsAsync(
                userId,
                cancellationToken);
            var subscriptions = userSubscriptions
                .Select(subscription => new UserSubscriptionResponse(
                    subscription.Id,
                    subscription.PlanName,
                    subscription.StartsAt,
                    subscription.EndsAt,
                    subscription.IsActive))
                .ToArray();
            return TypedResults.Ok(subscriptions);
        })
        .RequireAuthorization()
        .RequireRateLimiting("sensitive-write")
        .WithTags("Subscriptions")
        .WithName("GetMySubscriptions")
        .Produces<UserSubscriptionResponse[]>();

        app.MapGet("/api/payments/vnpay/ipn", async Task<IResult> (
            HttpContext httpContext,
            IPaymentGateway paymentGateway,
            IOrderFulfillmentService orderFulfillment,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var fields = httpContext.Request.Query.ToDictionary(
                item => item.Key,
                item => (string?)item.Value.ToString(),
                StringComparer.Ordinal);
            var notification = paymentGateway.VerifyNotification(fields);
            if (!notification.SignatureValid)
            {
                return TypedResults.Ok(new VnPayIpnResponse("97", "Invalid signature"));
            }

            if (string.IsNullOrWhiteSpace(notification.ProviderTransactionId) ||
                notification.AmountMinorUnits is null)
            {
                return TypedResults.Ok(new VnPayIpnResponse("99", "Invalid notification"));
            }

            var payment = await db.Payments
                .Include(item => item.Order)
                .SingleOrDefaultAsync(
                    item => item.Provider == paymentGateway.Provider &&
                        item.ProviderTxnId == notification.ProviderTransactionId,
                    cancellationToken);
            if (payment is null)
            {
                return TypedResults.Ok(new VnPayIpnResponse("01", "Payment not found"));
            }

            var expectedMinorUnits = checked((long)decimal.Round(
                payment.Amount * 100,
                0,
                MidpointRounding.AwayFromZero));
            if (notification.AmountMinorUnits != expectedMinorUnits ||
                !string.Equals(notification.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            {
                if (payment.MarkFailed())
                {
                    await db.SaveChangesAsync(cancellationToken);
                }

                return TypedResults.Ok(new VnPayIpnResponse("04", "Amount or currency mismatch"));
            }

            if (notification.ResponseCode != "00" || notification.TransactionStatus != "00")
            {
                if (payment.MarkFailed())
                {
                    await db.SaveChangesAsync(cancellationToken);
                }

                return TypedResults.Ok(new VnPayIpnResponse("00", "Payment result recorded"));
            }

            if (payment.MarkPaid())
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            var result = await orderFulfillment.VerifyAsync(
                payment.OrderId,
                reviewerUserId: null,
                note: $"VNPay transaction {notification.ProviderReference ?? notification.ProviderTransactionId}",
                cancellationToken);
            return result is OrderFulfillmentResult.Fulfilled or OrderFulfillmentResult.AlreadyFulfilled
                ? TypedResults.Ok(new VnPayIpnResponse("00", "Payment confirmed"))
                : TypedResults.Ok(new VnPayIpnResponse("04", "Order cannot be fulfilled"));
        })
        .AllowAnonymous()
        .RequireRateLimiting("sensitive-write")
        .WithTags("Payments")
        .WithName("VnPayIpn")
        .WithSummary("Verify a signed VNPay payment notification")
        .Produces<VnPayIpnResponse>();

        app.MapGet("/api/admin/dashboard", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var counts = new AdminDashboardResponse(
                await db.Users.CountAsync(cancellationToken),
                await db.Locations.CountAsync(
                    location => location.Status == LocationStatus.Active,
                    cancellationToken),
                await db.AudioFiles.CountAsync(audio => audio.IsActive, cancellationToken),
                await db.SubscriptionPlans.CountAsync(plan => plan.IsActive, cancellationToken),
                await db.SubscriptionOrders.CountAsync(
                    order => order.Status == SubscriptionOrderStatus.Pending,
                    cancellationToken));
            return TypedResults.Ok(counts);
        })
        .RequireAuthorization(policy => policy.RequireRole("Admin"))
        .RequireRateLimiting("sensitive-write")
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

    private static SubscriptionOrderResponse ToOrderResponse(
        SubscriptionOrder order,
        DateTimeOffset? manualQrReportedAt = null) =>
        new(
            order.Id,
            order.PlanId,
            order.PlanName,
            order.Amount,
            order.Currency,
            order.Status.ToString(),
            order.CreatedAt,
            order.ReviewedAt,
            order.ReviewNote,
            manualQrReportedAt);

    private static string AddQueryParameter(string uri, string key, string value)
    {
        var builder = new UriBuilder(uri);
        var query = builder.Query.TrimStart('?');
        var encoded = $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
        builder.Query = string.IsNullOrEmpty(query) ? encoded : $"{query}&{encoded}";
        return builder.Uri.ToString();
    }
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
    string? ReviewNote,
    DateTimeOffset? ManualQrReportedAt = null);

public sealed record QrPaymentClaimResponse(DateTimeOffset ReportedAt);

public sealed record AdminSubscriptionOrderResponse(
    Guid Id,
    string UserEmail,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    DateTimeOffset? ManualQrReportedAt);

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

public sealed record PaymentLinkResponse(Guid PaymentId, string PaymentUrl);

public sealed record VnPayIpnResponse(string RspCode, string Message);
