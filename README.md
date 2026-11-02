# FCG PaymentsAPI — FIAP Cloud Games | Fase 2

Microsserviço de **pagamentos simulados** da FIAP Cloud Games (FCG), desenvolvido com **.NET 8**, **MassTransit/RabbitMQ**, **Entity Framework Core/SQLite** e arquitetura em camadas.

> **Orquestração centralizada:** este repositório contém apenas a aplicação, seus testes e o Dockerfile. **Todos os manifestos Kubernetes (Deployment, Service, ConfigMap, Secret, PVC etc.) ficam exclusivamente no repositório `FCG-Orchestration`**. Não mantenha uma pasta `k8s/` neste repositório.

## 1. Responsabilidades

- Receber o evento `OrderPlacedEvent` enviado pelo CatalogAPI.
- Processar a compra usando um gateway fictício, com cenários configuráveis de aprovação, rejeição ou falha temporária.
- Persistir o pagamento e as tentativas no SQLite.
- Gravar o evento de saída em **Outbox** na mesma transação do resultado do pagamento.
- Publicar `PaymentProcessedEvent` para que CatalogAPI e NotificationsAPI concluam o fluxo de compra.
- Disponibilizar consulta de pagamento, health checks e recursos de diagnóstico em desenvolvimento.

**Não realiza cobranças reais nem armazena dados de cartão.**

## 2. Arquitetura e estrutura

```text
FCG_PaymentsAPI/
├── src/
│   ├── Payments.Domain/           # Entidades, regras e objetos de valor
│   ├── Payments.Application/      # Contratos, casos de uso e abstrações
│   ├── Payments.Infrastructure/   # EF Core, RabbitMQ, consumer, Outbox e gateway simulado
│   └── Payments.API/              # Inicialização, endpoints e configurações
├── tests/
│   └── Payments.Domain.Tests/     # Testes automatizados
├── Payments.sln
├── Dockerfile
├── .dockerignore
└── README.md
```

O repositório de infraestrutura (`FCG-Orchestration`) é independente deste repositório e centraliza Docker Compose, scripts e manifestos Kubernetes.

## 3. Fluxo de compra

```text
CatalogAPI -- OrderPlacedEvent --> RabbitMQ
                                      |
                                      v
                                 PaymentsAPI
                                      |
                         Gateway de pagamento simulado
                                      |
                               SQLite + Outbox
                                      |
                          PaymentProcessedEvent
                                      |
                                    RabbitMQ
                                      |
                     +----------------+----------------+
                     v                                 v
                 CatalogAPI                    NotificationsAPI
          (atualiza a biblioteca)        (simula confirmação de compra)
```

### Contrato de entrada

Namespace: `Fcg.Contracts`.

```csharp
public record OrderPlacedEvent(Guid OrderId, int UserId, int GameId, decimal Price);
```

### Contrato de saída

Namespace: `Fcg.Contracts`.

```csharp
public record PaymentProcessedEvent(
    Guid OrderId,
    int UserId,
    int GameId,
    decimal Price,
    string Status,
    DateTime ProcessedAtUtc,
    string? Reason);
```

Os valores finais de `Status` são `Approved` e `Rejected`. Os consumidores de CatalogAPI e NotificationsAPI precisam reconhecer os **mesmos contratos de mensagem** (inclusive namespace e tipos), possuir filas próprias e tratar possíveis eventos duplicados de forma idempotente. `FailureCode` e `TransactionId` estão disponíveis na consulta REST, mas **não** integram o contrato de saída acima.

## 4. Requisitos locais

- SDK **.NET 8** (ou versão compatível com os projetos).
- Docker Desktop para executar a aplicação em contêineres.
- RabbitMQ acessível, preferencialmente iniciado pelo `FCG-Orchestration`.
- Git e, para implantação, Kubernetes local habilitado no Docker Desktop e `kubectl`.
- Conta Docker Hub para publicação da imagem.

Confira as ferramentas no PowerShell:

```powershell
dotnet --version
docker --version
kubectl version --client
```

## 5. Configuração

A configuração padrão de desenvolvimento fica em `src/Payments.API/appsettings.json`. Este projeto será executado em **Development** e não exige JWT nessa modalidade quando `Jwt:Key` não estiver configurado. **Não publique senhas ou chaves reais nesse arquivo.** As variáveis de ambiente do .NET usam `__` para representar `:`.

| Variável | Função |
|---|---|
| `ConnectionStrings__PaymentsDb` | Caminho do banco SQLite, por exemplo `Data Source=./data/payments-v2.db` em execução local ou `Data Source=/app/data/payments-v2.db` no contêiner. |
| `RabbitMq__Host` | Hostname do RabbitMQ (`localhost` fora de contêineres; nome do serviço no Compose/Kubernetes). |
| `RabbitMq__Username` | Usuário RabbitMQ. |
| `RabbitMq__Password` | Senha RabbitMQ. |
| `RabbitMq__OrderQueue` | Fila de consumo de pedidos; padrão atual `payments-order-placed`. |
| `ASPNETCORE_ENVIRONMENT` | `Development` é o ambiente escolhido para esta entrega, com Swagger e endpoints de diagnóstico. |
| `PaymentSimulation__GatewayDelayMilliseconds` | Atraso artificial do gateway. |
| `PaymentSimulation__MaximumAttempts` | Configuração de tentativas da simulação. |
| `PaymentSimulation__RetryDelayMilliseconds` | Intervalo entre tentativas da simulação. |
| `PaymentSimulation__Rules__...` | Faixas de valores e cenários do gateway (configuradas em `appsettings.json`). |


### Regras de pagamento simuladas atuais

Conforme `src/Payments.API/appsettings.json` do projeto recebido:

| Valor da compra (R$) | Cenário |
|---|---|
| 0,01 a 499,99 | `Approved` |
| 500,00 a 699,99 | `InsufficientFunds` → `Rejected` |
| 700,00 a 799,99 | `InvalidCard` → `Rejected` |
| 800,00 a 899,99 | `GatewayUnavailable` → falha temporária |
| 900,00 a 999,99 | `Timeout` → falha temporária |
| 1.000,00 a 999.999,99 | `LimitExceeded` → `Rejected` |

Valores sem faixa correspondente são rejeitados pelo gateway simulado com `NO_MATCHING_RULE`. As falhas temporárias lançam exceções, acionando as tentativas de entrega do MassTransit; após esgotá-las, a mensagem pode ir para a fila `_error`, **sem** publicar um `Rejected` como se fosse uma recusa financeira.

## 6. Compilar e executar testes

Na raiz do repositório, no PowerShell:

```powershell
dotnet restore Payments.sln
dotnet build Payments.sln --no-restore
dotnet test Payments.sln --no-build
```

Para executar localmente a API, primeiro inicie um RabbitMQ acessível com as credenciais configuradas, depois execute:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\src\Payments.API\Payments.API.csproj --launch-profile Payments.API
```

O perfil existente define HTTP em `http://localhost:60632` e HTTPS em `https://localhost:60631`. A disponibilidade da URL HTTPS depende do certificado de desenvolvimento configurado. Para testes simples, use HTTP:

```powershell
Invoke-RestMethod http://localhost:60632/health
Invoke-RestMethod http://localhost:60632/ready
```

A compilação e os testes unitários não comprovam, isoladamente, o fluxo de mensageria de ponta a ponta; ele precisa de testes com o broker.

## 7. Endpoints HTTP

| Método e caminho | Disponibilidade | Objetivo |
|---|---|---|
| `GET /health` | Todos os ambientes | Liveness da aplicação. |
| `GET /ready` | Todos os ambientes | Verifica a conexão com SQLite. **Não testa o RabbitMQ.** |
| `GET /api/payments/{orderId}` | Todos os ambientes | Consulta pagamento; exige bearer JWT se a autenticação estiver habilitada. |
| `GET /api/payments/{orderId}/attempts` | Apenas `Development` | Consulta tentativas. |
| `GET /api/payments/stats` | Apenas `Development` | Estatísticas locais de pagamento. |
| `GET /dashboard` | Apenas `Development` | Painel simples de estatísticas. |
| `/swagger` | Apenas `Development` | Documentação interativa da API. |

Exemplo de consulta:

```powershell
$orderId = "SUBSTITUA_PELO_GUID_DA_COMPRA"
Invoke-RestMethod "http://localhost:60632/api/payments/$orderId"
```

**Segurança:** nesta entrega de desenvolvimento, o endpoint de consulta está sem autenticação se não houver `Jwt:Key`. Use somente em ambiente local/rede controlada. Para produção, seriam necessárias autenticação e autorização apropriadas.

## 8. Docker — build e publicação no Docker Hub

O `Dockerfile` na raiz utiliza **multi-stage build** (SDK .NET 8 para compilação e ASP.NET 8 para execução), porta interna **8080** e usuário não privilegiado.

Na raiz do `FCG_PaymentsAPI`, substitua `SEU_USUARIO` pelo usuário/organização do Docker Hub:

```powershell
dotnet test Payments.sln --no-build
docker build -t fcg-payments-api:2.0 -f Dockerfile .
docker login -u pripereiras
docker tag fcg-payments-api:2.0 pripereiras/fcg-payments-api:2.1
docker push pripereiras/fcg-payments-api:2.1
```

A imagem de entrega é **`pripereiras/fcg-payments-api:2.1`**. A tag deve coincidir com a referência usada no Compose e nos manifestos do `FCG-Orchestration`. O build local da versão 2.0 foi concluído; o push ao Docker Hub ainda precisa ser confirmado. Para conferir localmente:

```powershell
docker images pripereiras/fcg-payments-api
```

As imagens são armazenadas no **Docker Hub**, não no repositório Git de orquestração. O Git guarda as referências de imagem (`image: usuario/fcg-payments-api:tag`). Se o repositório Docker Hub for privado, o Kubernetes precisará de `imagePullSecrets` configurado pelo `FCG-Orchestration`.

## 9. Docker Compose e Kubernetes: execução centralizada

Todos os comandos de orquestração devem partir do repositório **`FCG-Orchestration`**, cuja documentação deve informar as variáveis de ambiente e os scripts disponíveis.

Fluxo esperado:

1. Publicar a imagem versionada do PaymentsAPI no Docker Hub.
2. No `FCG-Orchestration`, configurar a referência à **mesma tag** no Compose e no Deployment Kubernetes.
3. Criar os Secrets de execução sem versionar credenciais reais.
4. Executar o Compose ou aplicar os manifestos a partir do repositório de orquestração.
5. Validar Pod, Service, logs, health checks, acesso ao broker e processamento dos eventos.

Exemplos gerais, executados **na raiz do `FCG-Orchestration`** (dependem da configuração ali presente):

```powershell
docker compose pull
docker compose up -d
docker compose ps
```

Para acompanhar o Kubernetes após o deploy descrito no README de orquestração:

```powershell
kubectl get pods
kubectl get services
kubectl rollout status deployment/payments-api
kubectl logs deployment/payments-api --tail=100
```

**Não há comandos `kubectl apply` neste repositório**: os manifestos existem **somente** no `FCG-Orchestration`. Não execute `kubectl apply -f .` indiscriminadamente sem verificar a organização dos manifests e o tratamento de Secrets do repositório de orquestração.

## 10. Persistência e Outbox

A solução utiliza SQLite, incluindo as tabelas `Payments`, `Attempts` e `OutboxMessages`. O evento de saída é salvo junto com o pagamento, e um processo em segundo plano (`PaymentEventDispatcher`) tenta publicá-lo no RabbitMQ.

- O processamento utiliza `OrderId` para ajudar na idempotência.
- A entrega de eventos pode ocorrer **mais de uma vez**; CatalogAPI e NotificationsAPI precisam deduplicar.
- A implementação atual do dispatcher deve operar com **uma réplica** até que exista coordenação entre múltiplas réplicas.
- Em contêiner, persistir `/app/data` em volume. Em Kubernetes, o PVC é definido exclusivamente no `FCG-Orchestration`.

### Cuidado com mudanças no banco

O projeto chama `EnsureCreatedAsync()` na inicialização. Esse método cria o esquema inicial, mas **não executa migrações de modelos já existentes**. Para o modelo atual, o caminho padrão usa `payments-v2.db`. Se existir um banco anterior com outro esquema, faça backup e planeje migração antes da troca. **Não exclua o PVC para resolver incompatibilidades de schema.**

## 11. Como testar o fluxo de eventos

Após subir RabbitMQ e PaymentsAPI, produza um `OrderPlacedEvent` **com contrato e topologia compatíveis com MassTransit** (preferencialmente a partir do CatalogAPI ou de um pequeno produtor .NET de testes). Não basta publicar JSON arbitrário em uma fila sem os metadados esperados pelo MassTransit.

Use valores distintos para conferir os cenários:

| Valor de teste | Resultado esperado |
|---|---|
| `149.90` | `Approved` |
| `550.00` | `Rejected` (`INSUFFICIENT_FUNDS`) |
| `750.00` | `Rejected` (`INVALID_CARD`) |
| `850.00` | Retry/fila de erro (`GatewayUnavailable`) |
| `950.00` | Retry/fila de erro (`Timeout`) |
| `1500.00` | `Rejected` (`LIMIT_EXCEEDED`) |

Para cada teste, use um `OrderId` novo e confira:

1. O consumer recebeu a mensagem.
2. O pagamento foi persistido no SQLite quando houve resultado final.
3. A Outbox registrou a publicação pendente e/ou concluída.
4. `PaymentProcessedEvent` chegou ao consumidor correto (CatalogAPI/NotificationsAPI, quando integrados).
5. Reenvios com o mesmo `OrderId` não criam pagamentos duplicados.
6. Falhas temporárias aparecem nos logs e na fila de erro apropriada, sem aprovação indevida.

Esse roteiro é de **validação a executar**: não significa que os cenários foram testados com sucesso neste ambiente.

## 12. Solução de problemas

| Sintoma | O que verificar |
|---|---|
| `BrokerUnreachableException` | RabbitMQ ativo, endereço, porta, DNS, usuário e senha. Dentro de Kubernetes/Compose, `localhost` aponta para o próprio contêiner. |
| `NOT_AUTHORIZED` / `ACCESS_REFUSED` | Credenciais configuradas no broker versus variáveis da aplicação e Secrets. |
| `Jwt:Key é obrigatório fora de Development` | Confirmar que o ambiente de execução está configurado como `Development`, conforme o escopo acordado; não exponha o serviço sem autenticação na internet. |
| `/ready` retorna 503 | Arquivo SQLite, permissões e volume persistente. |
| `ImagePullBackOff` | Nome/tag publicados no Docker Hub, permissões do repositório e `imagePullSecrets` quando aplicável. |
| `no such table` / mudança de schema | Banco antigo incompatível; planejar migração. |
| Mensagem na fila `_error` | Examinar logs, exceções e cenário simulado antes de republicar. |
| Serviço não encontra RabbitMQ | Usar o nome do Service do Kubernetes, não `localhost`. |

## 13. Integração com os outros repositórios

- **UsersAPI:** autenticação/JWT e emissão de `UserCreatedEvent` (consumido pelo NotificationsAPI).
- **CatalogAPI:** publica `OrderPlacedEvent` e consome `PaymentProcessedEvent`.
- **PaymentsAPI:** consome o pedido e publica resultado do pagamento.
- **NotificationsAPI:** consome resultados de pagamento aprovados e simula o envio de confirmação de compra.
- **FCG-Orchestration:** único local de Docker Compose, manifestos Kubernetes e infraestrutura compartilhada.

Cada microsserviço deve ter seu próprio Dockerfile e manter autonomia do código; os contratos e o comportamento dos eventos precisam ser combinados entre o grupo.

## 14. Finalização e publicação

### Atualizar o repositório de código (PowerShell)

Na raiz do `FCG_PaymentsAPI`, após atualizar o código:

```powershell
git status
git add .
git status --short
git commit -m "seu commit aqui"
git push -u origin $(git branch --show-current)
```

O commit documenta o estado dos arquivos que estiverem incluídos. Verifique `git status` antes de confirmar para não adicionar segredos, bancos SQLite ou arquivos temporários.

### Publicar a imagem final no Docker Hub

O build local `fcg-payments-api:2.0` já foi realizado. Não há necessidade de rebuild apenas para alterar o nome da imagem:

```powershell
docker login -u pripereiras
docker tag fcg-payments-api:2.0 pripereiras/fcg-payments-api:2.1
docker push pripereiras/fcg-payments-api:2.1
```

Verifique a tag em <https://hub.docker.com/r/pripereiras/fcg-payments-api/tags>. Para confirmar que a imagem está disponível para novos ambientes, o ideal é realizar um `docker pull pripereiras/fcg-payments-api:2.1` em um ambiente sem a imagem local ou conferir a tag/digest no Docker Hub.

### Atualizar o FCG-Orchestration

O `Deployment` do PaymentsAPI, **apenas no repositório `FCG-Orchestration`**, deve referenciar a imagem:

```yaml
image: pripereiras/fcg-payments-api:2.1
imagePullPolicy: Always
```

O ambiente precisa fornecer `ASPNETCORE_ENVIRONMENT=Development`, `RabbitMq__Host` com o nome de Service do RabbitMQ, credenciais apropriadas e um volume persistente para `/app/data`. Valide as variáveis e os segredos antes de implantar. Não armazene imagens Docker binárias no Git.

---

**FIAP — Tech Challenge Fase 2 · FIAP Cloud Games (FCG)**
