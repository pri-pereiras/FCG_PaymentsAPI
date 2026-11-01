namespace Payments.Domain.Entities;
public sealed class PaymentAttempt
{
    private PaymentAttempt() { }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public int Number { get; private set; }
    public string Outcome { get; private set; } = "";
    public string? Detail { get; private set; }
    public DateTime AtUtc { get; private set; }
    public static PaymentAttempt Create(Guid orderId, int number, string outcome, string? detail) =>
        new() { Id = Guid.NewGuid(), OrderId = orderId, Number = number, Outcome = outcome, Detail = detail, AtUtc = DateTime.UtcNow };
}
