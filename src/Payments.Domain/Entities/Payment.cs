namespace Payments.Domain.Entities;
public sealed class Payment
{
    private Payment() { }
    public Guid PaymentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public int UserId { get; private set; }
    public int GameId { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public string Status { get; private set; } = PaymentStatus.Pending;
    public string? TransactionId { get; private set; }
    public string? FailureCode { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public static Payment Create(Guid orderId, Guid correlationId, int userId, int gameId, decimal price)
    {
        if (orderId == Guid.Empty || userId <= 0 || gameId <= 0) throw new ArgumentException("Pedido inválido.");
        if (price < 0 || decimal.Round(price, 2) != price) throw new ArgumentException("Valor inválido.");
        return new Payment { PaymentId = Guid.NewGuid(), OrderId = orderId, CorrelationId = correlationId == Guid.Empty ? orderId : correlationId,
            UserId = userId, GameId = gameId, Price = price, CreatedAtUtc = DateTime.UtcNow };
    }
    public void StartProcessing()
    {
        if (Status != PaymentStatus.Pending) throw new InvalidOperationException("Pagamento não está pendente.");
        Status = PaymentStatus.Processing;
    }
    public void Finish(bool approved, string? transactionId, string? failureCode, string? reason, int attempts)
    {
        if (Status != PaymentStatus.Processing) throw new InvalidOperationException("Pagamento não está em processamento.");
        if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
        Status = approved ? PaymentStatus.Approved : PaymentStatus.Rejected;
        TransactionId = transactionId; FailureCode = approved ? null : failureCode;
        Reason = approved ? null : reason; AttemptCount = attempts; ProcessedAtUtc = DateTime.UtcNow;
    }
}
