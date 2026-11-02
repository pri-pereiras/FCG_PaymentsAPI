using MassTransit;

namespace Fcg.Contracts;
[EntityName("FCG_CatalogAPI:OrderPlacedEvent")]
// CatalogAPI precisa publicar uma mensagem com este contrato e namespace MassTransit.
public record OrderPlacedEvent(Guid OrderId, int UserId, int GameId, decimal Price);
