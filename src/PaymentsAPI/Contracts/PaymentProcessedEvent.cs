namespace Fcg.Contracts;

// O CatalogAPI e o NotificationsAPI devem consumir este mesmo contrato.
public record PaymentProcessedEvent(
    Guid OrderId,
    int UserId,
    int GameId,
    decimal Price,
    string Status,
    DateTime ProcessedAtUtc,
    string? Reason);
