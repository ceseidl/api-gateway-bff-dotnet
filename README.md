English | [Português](README.pt-BR.md)

# api-gateway-bff-dotnet

> **Quick start**

```bash
dotnet build
# 4 terminals: see "Running" below
```

Needs only the .NET 10 SDK. Step by step in [Running](#running).

API Gateway + BFF (Backend for Frontend) in .NET, built with YARP and Minimal APIs. Code for the article "API Gateway + BFF in .NET (YARP, Minimal APIs)".

The solution (`ApiGatewayBff.slnx`) has two projects, both targeting `net10.0` (plus `LocalStubs`, a helper for running locally):

- **Gateway**: a reverse proxy based on [YARP](https://microsoft.github.io/reverse-proxy/) (`Yarp.ReverseProxy`). It validates JWT bearer tokens, applies a per-user rate limit (100 requests per minute, fixed window; falls back to the client IP, then to `anon`), forwards the `sub` claim to the downstream service as the `X-User-Id` header, and routes traffic by path prefix.
- **MobileBff**: a BFF for the mobile app, built with Minimal APIs. The `GET /order/{orderId}` endpoint calls the Order, Tracking and Notification services in parallel and returns a single aggregated response. If the secondary data (tracking, notifications) fails, the response is still returned with that data missing. If the order does not exist, it returns 404. Each HTTP client uses the standard resilience handler (`Microsoft.Extensions.Http.Resilience`).

## Gateway routes

Defined in `Gateway/appsettings.json`:

| Route | Path | Cluster | Notes |
|-------|------|---------|-------|
| `mobile` | `/mobile/{**catch-all}` | `mobile-bff` (2 destinations, round robin, active and passive health checks on `/health`) | Removes the `/mobile` prefix, sets `X-Client: mobile-app` |
| `web` | `/web/{**catch-all}` | `web-bff` (1 destination) | Removes the `/web` prefix |

Both routes use the `default` authorization policy and the `per-user` rate limiter policy.

## Running

Prerequisites: the .NET 10 SDK.

```bash
dotnet build
```

The default addresses in `appsettings.json` are Docker/Kubernetes-style host names. To run everything on one machine, this repo includes **LocalStubs**: fake Order, Tracking and Notification services plus a `/token` endpoint that mints a JWT. It is for local development only. Open four terminals at the repository root (Git Bash, or any shell that accepts `\` line continuation; in PowerShell put each command on one line):

**1. Fake downstream services (port 5100)**

```bash
dotnet run --project LocalStubs --urls http://localhost:5100
```

**2. MobileBff (port 5200)**

```bash
dotnet run --project MobileBff --urls http://localhost:5200 -- \
  --Services:Order=http://localhost:5100/ \
  --Services:Tracking=http://localhost:5100/ \
  --Services:Notification=http://localhost:5100/
```

**3. Gateway (port 5000)**, pointing at the BFF and trusting the stub token

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

**4. Call it**

```bash
TOKEN=$(curl -s http://localhost:5100/token)
curl -i http://localhost:5000/mobile/order/1                                     # 401
curl -i -H "Authorization: Bearer $TOKEN" http://localhost:5000/mobile/order/1   # 200
curl -i -H "Authorization: Bearer $TOKEN" http://localhost:5000/mobile/order/404 # 404
```

Expected results: `401` without a token, `200` with the aggregated order (order, tracking and notifications), and `404` for order `404`.

**Notes**

- The signing key in `LocalStubs/Program.cs` and in the command above is public and throwaway. Never reuse it.
- In a real deployment, set the same settings (`Authentication:Schemes:Bearer:*`) through configuration, with your identity provider's authority or signing key. The `web` route needs a `web-bff`, which is not part of this repository.
- If `dotnet build` fails on a private NuGet feed in your user-level `nuget.config`, add `--source https://api.nuget.org/v3/index.json`.

## Structure

```
ApiGatewayBff.slnx
Gateway/              YARP reverse proxy (JWT, rate limiting, routes in appsettings.json)
MobileBff/
  Clients/            HTTP clients for Order, Tracking and Notification
  Contracts/          DTOs and the aggregated response
  Endpoints/          Minimal API endpoints (GET /order/{orderId})
LocalStubs/           local-development fake services + /token (see Running)
LICENSE
```

## License

MIT. See [LICENSE](LICENSE). Author: Carlos Eduardo Seidl.
