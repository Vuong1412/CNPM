namespace QOL.Application.Commerce;

public interface IOrderFulfillmentService
{
    Task<OrderFulfillmentResult> VerifyAsync(
        Guid orderId,
        Guid? reviewerUserId,
        string? note,
        CancellationToken cancellationToken);
}

public enum OrderFulfillmentResult
{
    Fulfilled,
    AlreadyFulfilled,
    NotFound,
    Rejected
}
