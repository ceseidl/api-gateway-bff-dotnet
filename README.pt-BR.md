[English](README.md) | Português

[![CI](https://github.com/ceseidl/api-gateway-bff-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/ceseidl/api-gateway-bff-dotnet/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

# api-gateway-bff-dotnet

> **Início rápido**

```bash
dotnet build
# 4 terminais: veja "Como rodar" abaixo
```

Precisa só do SDK do .NET 10. Passo a passo em [Como rodar](#como-rodar).

API Gateway + BFF (Backend for Frontend) em .NET, construído com YARP e Minimal APIs. Código do artigo "API Gateway + BFF em .NET (YARP, Minimal APIs)".

A solution (`ApiGatewayBff.slnx`) tem dois projetos, ambos com `net10.0` (mais o `LocalStubs`, um auxiliar para rodar localmente):

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
```

Os endereços padrão do `appsettings.json` são nomes de host no estilo Docker/Kubernetes. Para rodar tudo em uma máquina, este repositório inclui o **LocalStubs**: serviços falsos de Pedido, Rastreio e Notificação, mais um endpoint `/token` que gera um JWT. É só para desenvolvimento local. Abra quatro terminais na raiz do repositório (Git Bash, ou outro shell que aceite `\` para quebra de linha; no PowerShell, deixe cada comando em uma linha):

**1. Serviços downstream falsos (porta 5100)**

```bash
dotnet run --project LocalStubs --urls http://localhost:5100
```

**2. MobileBff (porta 5200)**

```bash
dotnet run --project MobileBff --urls http://localhost:5200 -- \
  --Services:Order=http://localhost:5100/ \
  --Services:Tracking=http://localhost:5100/ \
  --Services:Notification=http://localhost:5100/
```

**3. Gateway (porta 5000)**, apontando para o BFF e confiando no token do stub

```bash
dotnet run --project Gateway --urls http://localhost:5000 -- \
  --ReverseProxy:Clusters:mobile-bff:Destinations:d1:Address=http://localhost:5200/ \
  --ReverseProxy:Clusters:mobile-bff:Destinations:d2:Address=http://localhost:5200/ \
  --Authentication:Schemes:Bearer:ValidIssuer=local-stubs \
  --Authentication:Schemes:Bearer:ValidAudiences:0=gateway-local \
  --Authentication:Schemes:Bearer:SigningKeys:0:Issuer=local-stubs \
  --Authentication:Schemes:Bearer:SigningKeys:0:Value=jR+Qxbo6tCH2VRzOvOEs5mV04l4QqKPBGrG2UMJguA8= \
  --Authentication:Schemes:Bearer:SigningKeys:0:Length=256
```

**4. Chame a API**

```bash
TOKEN=$(curl -s http://localhost:5100/token)
curl -i http://localhost:5000/mobile/order/1                                     # 401
curl -i -H "Authorization: Bearer $TOKEN" http://localhost:5000/mobile/order/1   # 200
curl -i -H "Authorization: Bearer $TOKEN" http://localhost:5000/mobile/order/404 # 404
```

Resultados esperados: `401` sem token, `200` com o pedido agregado (pedido, rastreio e notificações) e `404` para o pedido `404`.

**Observações**

- A chave de assinatura em `LocalStubs/Program.cs` e no comando acima é pública e descartável. Nunca a reutilize.
- Em um ambiente real, defina essas mesmas configurações (`Authentication:Schemes:Bearer:*`) por configuração, com a authority ou a chave de assinatura do seu provedor de identidade. A rota `web` precisa de um `web-bff`, que não faz parte deste repositório.
- Se o `dotnet build` falhar por causa de um feed NuGet privado no seu `nuget.config` de usuário, acrescente `--source https://api.nuget.org/v3/index.json`.

## Estrutura

```
ApiGatewayBff.slnx
Gateway/              Proxy reverso YARP (JWT, rate limiting, rotas no appsettings.json)
MobileBff/
  Clients/            HTTP clients de Order, Tracking e Notification
  Contracts/          DTOs e a resposta agregada
  Endpoints/          Endpoints Minimal API (GET /order/{orderId})
LocalStubs/           serviços falsos para desenvolvimento local + /token (veja Como rodar)
LICENSE
```

## Licença

MIT. Veja o [LICENSE](LICENSE). Autor: Carlos Eduardo Seidl.
