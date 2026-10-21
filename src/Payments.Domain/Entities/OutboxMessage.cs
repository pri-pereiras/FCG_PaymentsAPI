namespace Payments.Domain.Entities;
public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Payload { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int DeliveryAttempts { get; private set; }
    public string? LastError { get; private set; }
    public static OutboxMessage Create(Guid orderId, string payload) => new()
    { Id = Guid.NewGuid(), OrderId = orderId, Payload = payload, CreatedAtUtc = DateTime.UtcNow };
    public void Published() { PublishedAtUtc = DateTime.UtcNow; DeliveryAttempts++; LastError = null; }
    public void Failed(string error) { DeliveryAttempts++; LastError = error.Length > 500 ? error[..500] : error; }
}
