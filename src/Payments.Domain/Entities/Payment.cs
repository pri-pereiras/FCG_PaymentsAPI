namespace Payments.Domain.Entities;

public sealed class Payment
{
    private Payment() { } // EF Core
    public Guid OrderId { get; private set; }
    public int UserId { get; private set; }
    public int GameId { get; private set; }
    public decimal Price { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }
    public bool EventPublished { get; private set; }

    public static Payment Process(Guid orderId, int userId, int gameId, decimal price, decimal maximumApprovedAmount)
    {
        if (orderId == Guid.Empty || userId <= 0 || gameId <= 0)
            throw new ArgumentException("OrderId, UserId e GameId devem ser válidos.");
        if (maximumApprovedAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumApprovedAmount));

        var approved = price >= 0 && price <= maximumApprovedAmount;
        return new Payment
        {
            OrderId = orderId, UserId = userId, GameId = gameId, Price = price,
            Status = approved ? PaymentStatus.Approved : PaymentStatus.Rejected,
            Reason = approved ? null : price < 0 ? "Valor inválido" : "Pagamento recusado pela regra de simulação",
            ProcessedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkEventPublished() => EventPublished = true;
}
