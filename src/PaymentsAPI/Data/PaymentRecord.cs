namespace PaymentsAPI.Data;

public sealed class PaymentRecord
{
    public Guid OrderId { get; set; }
    public int UserId { get; set; }
    public int GameId { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ProcessedAtUtc { get; set; }
    public string? Reason { get; set; }
    public bool EventPublished { get; set; }
}
