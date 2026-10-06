using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// EN: LOCAL DEVELOPMENT ONLY. Fake Order/Tracking/Notification services and a tiny identity
// EN: provider, so the Gateway and the MobileBff can run on one machine without real services.
// PT: SOMENTE PARA DESENVOLVIMENTO LOCAL. Serviços falsos de Pedido/Rastreio/Notificação e um
// PT: provedor de identidade mínimo, para rodar o Gateway e o MobileBff em uma máquina sem serviços reais.

// EN: A new RSA key pair is generated on every start. The private key never leaves this process;
// EN: the Gateway only fetches the public key (/jwks), exactly as with a real identity provider.
// PT: Um novo par de chaves RSA é gerado a cada execução. A chave privada nunca sai deste processo;
// PT: o Gateway só busca a chave pública (/jwks), exatamente como com um provedor de identidade real.
using var rsa = RSA.Create(2048);
var keyId = Guid.NewGuid().ToString("N");
var signingKey = new RsaSecurityKey(rsa) { KeyId = keyId };
var publicKey = rsa.ExportParameters(includePrivateParameters: false);

const string Audience = "gateway-local";

var app = WebApplication.CreateBuilder(args).Build();

static string BaseUrl(HttpRequest request) =>
    $"{request.Scheme}://{request.Host}";

app.MapGet("/health", () => Results.Ok());

app.MapGet("/orders/{id}", (string id) =>
    id == "404"
        ? Results.NotFound()
        : Results.Ok(new { id, status = "Paid", total = 120.50m }));

// EN: The id "slow" never answers: it exercises the BFF timeout handling (partial failure).
// PT: O id "slow" nunca responde: exercita o tratamento de timeout do BFF (falha parcial).
app.MapGet("/tracking/{id}", async (string id, CancellationToken ct) =>
{
    if (id == "slow")
        await Task.Delay(Timeout.Infinite, ct);

    return Results.Ok(new { status = "InTransit", location = "Curitiba" });
});

app.MapGet("/notifications/{id}", (string id) =>
    Results.Ok(new[] { new { text = "Order paid / Pedido pago" } }));

// EN: OpenID Connect discovery document: tells the Gateway where the public keys are.
// PT: Documento de discovery do OpenID Connect: diz ao Gateway onde estão as chaves públicas.
app.MapGet("/.well-known/openid-configuration", (HttpRequest request) =>
    Results.Ok(new
    {
        issuer = BaseUrl(request),
        jwks_uri = $"{BaseUrl(request)}/jwks",
    }));

// EN: The public key only (JWKS).
// PT: Somente a chave pública (JWKS).
app.MapGet("/jwks", () =>
    Results.Ok(new
    {
        keys = new[]
        {
            new
            {
                kty = "RSA",
                use = "sig",
                alg = SecurityAlgorithms.RsaSha256,
                kid = keyId,
                n = Base64UrlEncoder.Encode(publicKey.Modulus),
                e = Base64UrlEncoder.Encode(publicKey.Exponent),
            },
        },
    }));

// EN: Mints a short-lived JWT for the "sub" given in the query string (default: user-42).
// PT: Gera um JWT de curta duração para o "sub" informado na query string (padrão: user-42).
app.MapGet("/token", (HttpRequest request, string? sub) =>
{
    var descriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim("sub", sub ?? "user-42")]),
        Issuer = BaseUrl(request),
        Audience = Audience,
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
    };
    return Results.Text(new JsonWebTokenHandler().CreateToken(descriptor));
});

app.Run();
