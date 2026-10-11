namespace Fcg.Contracts;
// CatalogAPI e NotificationsAPI consomem este contrato.
public record PaymentProcessedEvent(Guid OrderId, int UserId, int GameId, decimal Price,
    string Status, DateTime ProcessedAtUtc, string? Reason);
