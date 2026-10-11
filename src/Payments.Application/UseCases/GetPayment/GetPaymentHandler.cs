using Payments.Application.Abstractions;
using Payments.Domain.Entities;
namespace Payments.Application.UseCases.GetPayment;
public sealed class GetPaymentHandler(IPaymentRepository repository)
{
    public Task<Payment?> HandleAsync(Guid orderId, CancellationToken ct) => repository.GetByOrderIdAsync(orderId, ct);
}
