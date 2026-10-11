namespace Fcg.Contracts;

// CONTRATO COMPARTILHADO: o CatalogAPI deve publicar este mesmo tipo de mensagem.
public record OrderPlacedEvent(Guid OrderId, int UserId, int GameId, decimal Price);
