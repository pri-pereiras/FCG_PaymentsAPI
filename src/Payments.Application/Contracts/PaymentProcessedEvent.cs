using MassTransit;

namespace Fcg.Contracts;
// CatalogAPI e NotificationsAPI consomem este contrato.

[EntityName("FCG_PaymentsAPI:PaymentProcessedEvent")]
public record PaymentProcessedEvent(Guid OrderId, int UserId, int GameId, decimal Price,
    string Status, DateTime ProcessedAtUtc, string? Reason);
