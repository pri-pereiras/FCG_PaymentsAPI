# FCG PaymentsAPI — Clean Architecture v2

Microsserviço da Fase 2 da FIAP Cloud Games. **.NET 8**, MassTransit + RabbitMQ, EF Core/SQLite, gateway simulado, transação para **Payment + Attempts + Outbox**, diagnóstico e implantação Docker/Kubernetes.

## Camadas

- `Payments.Domain`: ciclo de estados, entidade Payment, PaymentAttempt, OutboxMessage, Money.
- `Payments.Application`: contratos compatíveis com CatalogAPI/NotificationsAPI, casos de uso, abstrações IPaymentGateway e IPaymentRepository.
- `Payments.Infrastructure`: FakePaymentGateway, EF Core, consumer MassTransit, dispatcher Outbox.
- `Payments.API`: consulta REST, health checks e painel local.
- `tests`: testes de domínio, integração SQLite e cenários simulados.

## Contratos (inalterados)

`Fcg.Contracts.OrderPlacedEvent(Guid OrderId, int UserId, int GameId, decimal Price)` recebido da CatalogAPI.

`Fcg.Contracts.PaymentProcessedEvent(Guid OrderId, int UserId, int GameId, decimal Price, string Status, DateTime ProcessedAtUtc, string? Reason)` publicado para CatalogAPI e NotificationsAPI. **Somente Approved/Rejected são publicados.**

⚠️ O consumidor dos demais serviços precisa utilizar o mesmo namespace e propriedades, filas individuais e idempotência por OrderId. O contrato publicado não inclui FailureCode/TransactionId, que ficam na consulta da PaymentsAPI.

## Cenários do gateway fictício

| Valor BRL | Resultado |
|---|---|
| 149.90 | Approved |
| 1500.00 | Rejected (`LIMIT_EXCEEDED`, limite configurável 1000) |
| 777.01 | Rejected (`INSUFFICIENT_FUNDS`) |
| 777.02 | Rejected (`INVALID_CARD`) |
| 777.03 | Exceção transitória simulada (`_error` após retries) |
| 777.04 | Timeout simulado (`_error` após retries) |

Todos os valores devem ter até duas casas decimais; não há cartão real, dinheiro real nem integração financeira. Falhas temporárias **não** se tornam `Rejected`: o MassTransit faz retry e, se esgotado, envia à fila de erro. `PaymentAttempt` guarda as tentativas que resultaram em processamento concluído; falhas que impedem o commit ficam nos logs / fila de erro. Cenários são **por valor para fins pedagógicos**, não um mecanismo apropriado para produção.

## Executar

```powershell
dotnet restore Payments.sln
dotnet build Payments.sln
dotnet test Payments.sln
docker build -t fcg-payments-api:2.0 .
```

No quinto repositório `FCG-Orchestration`, atualize a tag da imagem de `fcg-payments-api:1.0` para `:2.0`, configure um Secret real **com credenciais idênticas às do RabbitMQ**, e aplique os manifestos da PaymentsAPI individualmente, **sem aplicar `secret.example.yaml`**.

```cmd
kubectl apply -f ..\FCG_PaymentsAPI\k8s\configmap.yaml
kubectl apply -f ..\FCG_PaymentsAPI\k8s\pvc.yaml
kubectl apply -f ..\FCG_PaymentsAPI\k8s\service.yaml
kubectl apply -f ..\FCG_PaymentsAPI\k8s\deployment.yaml
kubectl rollout status deployment/payments-api
kubectl port-forward service/payments-api 8083:80
```

Atualize o Deployment para usar `fcg-payments-api:2.0` **antes** do apply. Em Development: `/swagger`, `/dashboard`, `/api/payments/stats`, `/api/payments/{orderId}/attempts`; em todos os ambientes: `/health`, `/ready`, `/api/payments/{orderId}`.

## ATENÇÃO: evolução do banco existente

**Houve mudança de schema!** O modelo agora possui três tabelas (`Payments`, `Attempts`, `OutboxMessages`). `EnsureCreatedAsync` **não migra** SQLite já existente. Não aponte o v2 ao arquivo `payments.db` anterior: defina `ConnectionStrings__PaymentsDb=Data Source=/app/data/payments-v2.db` no Secret `payments-api-secret` (o exemplo foi atualizado). O PVC antigo permanece intacto, mas pagamentos anteriores não aparecem no novo banco. Em ambiente com dados a preservar, crie e teste uma migração EF Core/ETL antes de trocar a aplicação, com backup. **Nunca apague o PVC para atualizar o schema.**

## Configurações

- `ConnectionStrings__PaymentsDb`: caminho do SQLite, por exemplo `/app/data/payments-v2.db`.
- `RabbitMq__Host`, `RabbitMq__Username`, `RabbitMq__Password`, `RabbitMq__OrderQueue`.
- `PaymentSimulation__MaximumApprovedAmount`, `GatewayDelayMilliseconds`, `MaximumAttempts`, `RetryDelayMilliseconds`.
- `ASPNETCORE_ENVIRONMENT`: `Development` libera Swagger, painel e endpoints de diagnóstico; `Production` não disponibiliza esses endpoints.
- `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`: **obrigatórios fora de Development**, JWT HS256 >= 32 bytes. Consultas em Production exigem bearer token. O emissor/segredo devem ser compatíveis com UsersAPI. O endpoint `GET /api/payments/{orderId}` não valida por conta própria a propriedade do pedido; implemente política de autorização por usuário antes de expor a clientes externos.

## Garantias e limitações

- A gravação da compra, tentativas e Outbox é atômica. OrderId é chave única para idempotência persistente.
- A entrega é **at-least-once**: em falha depois da publicação e antes do checkpoint, o evento poderá repetir. CatalogAPI / NotificationsAPI precisam deduplicar por OrderId.
- `PaymentEventDispatcher` pressupõe **uma única réplica**, como no Deployment fornecido. Antes de escalar, adicionar claim/lease ou usar o EF transactional outbox integrado do MassTransit com concorrência controlada.
- `Pending` / `Processing` são transições em memória, confirmadas no armazenamento somente com resultado final. Não há recuperação de pagamento parcial durável.
- A interface Money está pronta para futuras regras de valor/moeda; o contrato atual usa `decimal`/BRL.
- JWT/segredos reais e dashboards autenticados requerem revisão adicional antes de produção.
- O arquivo `secret.example.yaml` é somente um exemplo; **não executar `kubectl apply -f k8s/`** sem removê-lo ou gerenciar segredo de forma segura.
- Não há testes reais de broker nesta entrega: os testes de integração cobrem persistência SQLite e casos de uso; validar RabbitMQ/Kubernetes no ambiente local.
