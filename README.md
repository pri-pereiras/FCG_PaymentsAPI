# FCG PaymentsAPI — Clean Architecture (.NET 8)

Microsserviço de pagamentos simulados. Consome `OrderPlacedEvent` do CatalogAPI no RabbitMQ; persiste o resultado e publica `PaymentProcessedEvent` para CatalogAPI e NotificationsAPI.

## Camadas e dependências

```text
src/
  Payments.Domain/           # Entidade Payment e status; sem dependências externas
  Payments.Application/      # Casos de uso, interfaces/ports, contratos de eventos
  Payments.Infrastructure/   # EF Core/SQLite, RabbitMQ/MassTransit, dispatcher outbox
  Payments.API/              # HTTP, Swagger, DI e composição da aplicação
```

Dependências: `API -> Infrastructure -> Application -> Domain`. Infrastructure implementa as interfaces definidas em Application. Domain não conhece mensageria nem banco. **FCG-Orchestration é o quinto repositório**, centraliza `docker-compose.yml`, RabbitMQ e scripts de implantação. Cada API conserva seu `Dockerfile` e a pasta `/k8s` exigida pelo trabalho.

## Regras da simulação

- `0 <= Price <= PaymentSimulation:MaximumApprovedAmount` → `Approved`.
- Outros valores → `Rejected` (preço negativo recebe motivo específico).
- Chave `OrderId` é única: reentregas são idempotentes para registro do pagamento.
- O dispatcher publica eventos pendentes em loop; modelo `at-least-once`: CatalogAPI/NotificationsAPI devem deduplicar por `OrderId`.
- Não há cobrança real. SQLite + `EnsureCreated` servem ao MVP; para produção usar migrations, banco transacional escalável e outbox com coordenação entre instâncias.

## Contratos MassTransit

`Fcg.Contracts.OrderPlacedEvent(Guid OrderId, int UserId, int GameId, decimal Price)`.

`Fcg.Contracts.PaymentProcessedEvent(Guid OrderId, int UserId, int GameId, decimal Price, string Status, DateTime ProcessedAtUtc, string? Reason)`.

Os consumidores/publicadores nos outros microsserviços devem usar o mesmo **namespace, nome e tipos** dos contratos. Idealmente manter pacote compartilhado versionado (sem compartilhamento da lógica de domínio).

## Como executar

Ponha os repositórios lado a lado (`PaymentsAPI`, `FCG-Orchestration`) e rode `docker compose up --build -d` dentro de `FCG-Orchestration` (ajuste a pasta do projeto conforme nome de clone). Endpoint: `http://localhost:8082/health` e consulta `GET /api/payments/{orderId}`. RabbitMQ management: `http://localhost:15672`.

Ou no diretório deste repositório: `dotnet restore Payments.sln` e `dotnet run --project src/Payments.API` (necessário RabbitMQ acessível na configuração).

## Configuração

- `ConnectionStrings__PaymentsDb`: connection string do SQLite (`Data Source=/app/data/payments.db` em Docker).
- `RabbitMq__Host`, `RabbitMq__Username`, `RabbitMq__Password`: broker e credenciais.
- `RabbitMq__OrderQueue`: fila de pedidos (padrão `payments-order-placed`).
- `PaymentSimulation__MaximumApprovedAmount`: valor máximo de aprovação (padrão 1000).

## Kubernetes

O diretório `k8s` possui `deployment.yaml`, `service.yaml`, `configmap.yaml`, `pvc.yaml`, `secret.example.yaml`. Crie o Secret sem commitar credenciais. Para execução local, o script de orquestração aplica manifests e exige a imagem `payments-api:local` no cluster (no Kind, carregue via `kind load docker-image payments-api:local`). Configuração do RabbitMQ deve coincidir com Secret do broker. O Deployment limita a uma réplica pelo uso de SQLite e PVC.

## Limitações e próximos passos

Ainda é preciso integrar CatalogAPI e NotificationsAPI, executar build/testes com .NET e Docker em seu computador e confirmar os fluxos end-to-end. O endpoint `/health` apenas confirma processo HTTP; não testa conectividade com broker. Em produção, adicionar testes automatizados, migrations, observabilidade, proteção do endpoint de consulta e outbox transacional em ambiente distribuído.
