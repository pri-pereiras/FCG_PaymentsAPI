> **Orquestração:** o `docker-compose.yml` da Fase 2 foi centralizado no quinto repositório `FCG-Orchestration`. O `Dockerfile` e a pasta `k8s/` continuam neste repositório conforme exigência do enunciado.

# PaymentsAPI — FIAP Cloud Games / Tech Challenge Fase 2

Microsserviço independente em **.NET 8** que recebe `OrderPlacedEvent` via RabbitMQ, simula pagamento, persiste o resultado e publica `PaymentProcessedEvent` para Catálogo e Notificações.

## Relação com a Fase 1

O projeto original usava uma aplicação monolítica `TechChallenge` (camadas Domain, Infrastructure e API) e o método `UserGameService.AddGameToLibraryAsync()` incluía jogos imediatamente na biblioteca. Na Fase 2, o `CatalogAPI` **não deve inserir na biblioteca antes do evento Approved**.

## Estrutura

```text
PaymentsAPI/
├── src/PaymentsAPI/
│   ├── Contracts/              # Eventos (mesmo namespace e formato nos outros serviços)
│   ├── Consumers/              # Recebe OrderPlacedEvent
│   ├── Data/                   # SQLite: pagamentos e resultados pendentes
│   ├── Options/                # Regra simulada parametrizada
│   ├── Services/               # Publicação assíncrona de PaymentProcessedEvent
│   ├── Program.cs              # DI, MassTransit, endpoints, Swagger
│   ├── appsettings.json
│   └── PaymentsAPI.csproj
├── k8s/                       # Deployment, Service, ConfigMap, Secret de exemplo, PVC
├── Dockerfile
└── README.md  # execução via ../FCG-Orchestration/docker-compose.yml
```

## Contratos obrigatórios para integração

Este serviço usa `Fcg.Contracts` com tipos C# **idênticos** nos serviços envolvidos. Em um projeto real, recomenda-se publicar um pacote NuGet versionado de contratos; cada microsserviço mantém o seu repositório independente.

```csharp
namespace Fcg.Contracts;
public record OrderPlacedEvent(Guid OrderId, int UserId, int GameId, decimal Price);
public record PaymentProcessedEvent(
    Guid OrderId, int UserId, int GameId, decimal Price,
    string Status, DateTime ProcessedAtUtc, string? Reason);
```

- `OrderId` é um `Guid` **único por tentativa de compra**, produzido pelo Catálogo. A Fase 2 exige `UserId`, `GameId` e `Price`; `OrderId` foi acrescentado para rastreamento e idempotência.
- `CatalogAPI`: publicar `OrderPlacedEvent` ao iniciar uma compra; consumir `PaymentProcessedEvent`; se `Status == "Approved"`, inserir na biblioteca **apenas uma vez por OrderId**. Rejected não adiciona o jogo.
- `NotificationsAPI`: consumir `PaymentProcessedEvent`; se Approved, registrar a simulação do e-mail uma única vez por OrderId.
- O RabbitMQ/MassTransit cria exchanges por tipo de mensagem e vincula a fila `payments-order-placed` ao evento consumido; os consumidores do resultado devem ter **filas separadas** entre si (pub/sub), não uma fila compartilhada.

## Simulação do pagamento

Regras **sugeridas para esta implementação**, não definidas pelo enunciado:
- `0 <= Price <= PaymentSimulation:MaximumApprovedAmount` → `Approved`.
- Valor negativo ou preço acima do limite → `Rejected`.
- O padrão é 1000, e pode ser alterado por configuração.
- `UserId`, `GameId` e `OrderId` inválidos geram falha técnica; não são uma rejeição de cartão.
- Não existe gateway real, cartão, PIX ou cobrança.

## Executar com Docker Compose

Requisitos: Docker com Compose. A partir do repositório irmão `FCG-Orchestration`:

```bash
cd ../FCG-Orchestration
cp .env.example .env  # No PowerShell: Copy-Item .env.example .env
docker compose up --build -d
docker compose logs -f payments-api
```

- Swagger: http://localhost:8082/swagger
- Saúde: http://localhost:8082/health
- RabbitMQ UI: http://localhost:15672 (credenciais definidas no arquivo .env da orquestração)
- Consulta: `GET http://localhost:8082/api/payments/{orderId}`

**Importante:** o quinto repositório sobe atualmente PaymentsAPI + RabbitMQ. O grupo deve adicionar UsersAPI, CatalogAPI e NotificationsAPI para atender ao requisito de execução integral.

## Executar com dotnet local

1. Em `../FCG-Orchestration`, subir o RabbitMQ: `docker compose up -d rabbitmq`. Ao executar a API localmente, configure as credenciais para coincidir com o arquivo `.env`.
2. `dotnet run --project src/PaymentsAPI/PaymentsAPI.csproj --urls http://localhost:8082`.
3. A API criará `./data/payments.db` relativo ao diretório de execução.

Não há endpoint público de pagamento: o gatilho é **somente o evento recebido**.

## Publicar evento de teste

O modo mais fiel é usar o CatalogAPI integrado. Alternativamente, crie um pequeno produtor .NET referenciando `MassTransit.RabbitMQ` e os contratos de `src/PaymentsAPI/Contracts/`:

```csharp
// Após configurar MassTransit e injetar IPublishEndpoint no produtor:
var orderId = Guid.NewGuid();
await publisher.Publish(new OrderPlacedEvent(orderId, 1, 2, 79.90m));
// Checar GET /api/payments/{orderId} e os logs do resultado.
```

Também envie um pedido com `Price = 1001m` para testar `Rejected`. O `Price` real deve vir do catálogo confiável do servidor e não diretamente do corpo da solicitação do cliente.

## Variáveis de ambiente

| Variável | Finalidade | Exemplo |
|---|---|---|
| `RabbitMq__Host` | DNS do broker | `rabbitmq` |
| `RabbitMq__Username` | Usuário do broker | `guest` (somente local) |
| `RabbitMq__Password` | Senha do broker | secret |
| `RabbitMq__OrderQueue` | Fila consumidora | `payments-order-placed` |
| `ConnectionStrings__PaymentsDb` | SQLite local | `Data Source=/app/data/payments.db` |
| `PaymentSimulation__MaximumApprovedAmount` | Limite da simulação | `1000` |

## Kubernetes local

Os manifestos em `k8s/` pressupõem que **RabbitMQ já esteja instalado e acessível pelo Service `rabbitmq`**, com usuário/senha conhecidos, e que a imagem `payments-api:local` esteja disponível no cluster.

```bash
docker build -t payments-api:local .
# Kind: kind load docker-image payments-api:local --name <nome-do-cluster>
# Minikube: minikube image load payments-api:local
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/pvc.yaml
# Ajuste o exemplo com credenciais reais do broker local antes de aplicar:
kubectl apply -f k8s/secret.example.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/deployment.yaml
kubectl get pods
kubectl logs deployment/payments-api
kubectl port-forward service/payments-api 8082:80
```

Para `kubectl apply -f k8s/` funcionar como demonstrado no enunciado, configure as credenciais e o broker do cluster. **Não comite Secrets reais**: `secret.example.yaml` é somente modelo de laboratório. SQLite usa PVC e **uma réplica**: para escalabilidade real, trocar por banco externo e mecanismo de outbox transacional próprio para múltiplas réplicas.

## Confiabilidade e limitações

- `OrderId` é chave primária. Redelivery de uma compra não a processa outra vez.
- O evento de saída é persistido como pendente antes da publicação; um worker publica e marca como enviado após sucesso (outbox simplificada).
- Se a publicação ocorrer e o processo morrer antes de marcar a linha, `PaymentProcessedEvent` pode ser publicado novamente. **Catálogo e Notificações precisam deduplicar por OrderId**.
- Nesta implementação MVP, o endpoint `/health` apenas confirma que o processo HTTP está vivo, **não** verifica RabbitMQ/banco.
- O consumidor não autentica o produtor por conta própria: configurar credenciais/ACL e isolamento do broker, e nunca confiar em preço vindo de um cliente final.
- Para ambiente de produção: migrations, banco remoto, estratégia transacional de outbox, observabilidade, autorização no endpoint de consulta e testes automatizados de integração.

## Checklist da Fase 2

- [x] Projeto independente .NET 8
- [x] Consumo de `OrderPlacedEvent`
- [x] Simulação `Approved`/`Rejected`
- [x] Publicação de `PaymentProcessedEvent`
- [x] Dockerfile multi-stage
- [x] Deployment, Service, ConfigMap, Secret (exemplo), PVC
- [x] README com instruções e variáveis
- [ ] Integrar contratos e filas nos serviços CatalogAPI / NotificationsAPI
- [ ] Integrar Compose dos quatro serviços
- [ ] Testar no Docker e Kubernetes locais
- [ ] Gravar vídeo de demonstração e publicar repositório Git individual
