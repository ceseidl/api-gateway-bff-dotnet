English | [Português](README.pt-BR.md)

# api-gateway-bff-dotnet

API Gateway + BFF (Backend for Frontend) in .NET, built with YARP and Minimal APIs. Code for the article "API Gateway + BFF in .NET (YARP, Minimal APIs)".

The solution (`ApiGatewayBff.slnx`) has two projects, both targeting `net10.0`:

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
dotnet run --project Gateway
dotnet run --project MobileBff
```

Notes on configuration (the default values are Docker/Kubernetes-style host names, not local ones):

- `MobileBff/appsettings.json` expects the downstream services at `http://order-service:8080/`, `http://tracking-service:8080/` and `http://notification-service:8080/` (section `Services`). These services are not part of this repository; override the addresses to run locally.
- `Gateway/appsettings.json` points the clusters at `http://mobile-bff-1:8080/`, `http://mobile-bff-2:8080/` and `http://web-bff:8080/`. The `web-bff` is not part of this repository.
- JWT validation is registered with `AddJwtBearer()` and no options in code, and `appsettings.json` has no authentication section, so the token validation settings (for example the authority or signing key) must be supplied through configuration before the Gateway can validate tokens.

## Structure

```
ApiGatewayBff.slnx
Gateway/              YARP reverse proxy (JWT, rate limiting, routes in appsettings.json)
MobileBff/
  Clients/            HTTP clients for Order, Tracking and Notification
  Contracts/          DTOs and the aggregated response
  Endpoints/          Minimal API endpoints (GET /order/{orderId})
LICENSE
```

## License

MIT. See [LICENSE](LICENSE). Author: Carlos Eduardo Seidl.
