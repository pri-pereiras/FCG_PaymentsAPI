namespace PaymentsAPI.Options;

public sealed class PaymentOptions
{
    public decimal MaximumApprovedAmount { get; set; } = 1000m;
}
