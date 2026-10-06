[English](README.md) | Português

# api-gateway-bff-dotnet

API Gateway + BFF (Backend for Frontend) em .NET, construído com YARP e Minimal APIs. Código do artigo "API Gateway + BFF em .NET (YARP, Minimal APIs)".

A solution (`ApiGatewayBff.slnx`) tem dois projetos, ambos com `net10.0`:

- **Gateway**: um proxy reverso baseado em [YARP](https://microsoft.github.io/reverse-proxy/) (`Yarp.ReverseProxy`). Valida tokens JWT bearer, aplica rate limit por usuário (100 requisições por minuto, janela fixa; usa o IP do cliente como alternativa e depois `anon`), repassa a claim `sub` ao serviço downstream no header `X-User-Id` e roteia o tráfego por prefixo de path.
- **MobileBff**: um BFF para o app mobile, feito com Minimal APIs. O endpoint `GET /order/{orderId}` chama os serviços de Order, Tracking e Notification em paralelo e devolve uma única resposta agregada. Se os dados secundários (tracking, notificações) falharem, a resposta ainda é devolvida sem esses dados. Se o pedido não existir, retorna 404. Cada HTTP client usa o resilience handler padrão (`Microsoft.Extensions.Http.Resilience`).

## Rotas do Gateway

Definidas em `Gateway/appsettings.json`:

| Rota | Path | Cluster | Observações |
|------|------|---------|-------------|
| `mobile` | `/mobile/{**catch-all}` | `mobile-bff` (2 destinos, round robin, health checks ativo e passivo em `/health`) | Remove o prefixo `/mobile`, define `X-Client: mobile-app` |
| `web` | `/web/{**catch-all}` | `web-bff` (1 destino) | Remove o prefixo `/web` |

As duas rotas usam a política de autorização `default` e a política de rate limiter `per-user`.

## Como rodar

Pré-requisito: o SDK do .NET 10.

```bash
dotnet build
dotnet run --project Gateway
dotnet run --project MobileBff
```

Notas sobre configuração (os valores padrão são nomes de host no estilo Docker/Kubernetes, não locais):

- `MobileBff/appsettings.json` espera os serviços downstream em `http://order-service:8080/`, `http://tracking-service:8080/` e `http://notification-service:8080/` (seção `Services`). Esses serviços não fazem parte deste repositório; sobrescreva os endereços para rodar localmente.
- `Gateway/appsettings.json` aponta os clusters para `http://mobile-bff-1:8080/`, `http://mobile-bff-2:8080/` e `http://web-bff:8080/`. O `web-bff` não faz parte deste repositório.
- A validação de JWT é registrada com `AddJwtBearer()` sem opções no código, e o `appsettings.json` não tem seção de autenticação; portanto, as configurações de validação do token (por exemplo authority ou chave de assinatura) precisam ser fornecidas via configuração para o Gateway conseguir validar tokens.

## Estrutura

```
ApiGatewayBff.slnx
Gateway/              Proxy reverso YARP (JWT, rate limiting, rotas no appsettings.json)
MobileBff/
  Clients/            HTTP clients de Order, Tracking e Notification
  Contracts/          DTOs e a resposta agregada
  Endpoints/          Endpoints Minimal API (GET /order/{orderId})
LICENSE
```

## Licença

MIT. Veja o [LICENSE](LICENSE). Autor: Carlos Eduardo Seidl.
