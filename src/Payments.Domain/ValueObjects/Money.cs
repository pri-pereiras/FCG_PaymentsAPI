namespace Payments.Domain.ValueObjects;
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public Money(decimal amount, string currency = "BRL")
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (decimal.Round(amount, 2) != amount) throw new ArgumentException("Valor deve conter no máximo duas casas decimais.");
        if (currency != "BRL") throw new ArgumentException("A simulação aceita apenas BRL.");
        Amount = amount; Currency = currency;
    }
}
