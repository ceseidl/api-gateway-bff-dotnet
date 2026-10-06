using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// EN: LOCAL DEVELOPMENT ONLY. Fake Order/Tracking/Notification services and a token endpoint,
// EN: so the Gateway and the MobileBff can run on one machine without the real downstream services.
// PT: SOMENTE PARA DESENVOLVIMENTO LOCAL. Serviços falsos de Pedido/Rastreio/Notificação e um endpoint
// PT: de token, para rodar o Gateway e o MobileBff em uma máquina sem os serviços downstream reais.

// EN: Public, throwaway key shared with the Gateway through its command-line arguments (see README).
// EN: Never use it anywhere else.
// PT: Chave pública e descartável, compartilhada com o Gateway por argumentos de linha de comando (veja o README).
// PT: Nunca use em outro lugar.
const string DevSigningKey = "jR+Qxbo6tCH2VRzOvOEs5mV04l4QqKPBGrG2UMJguA8=";
const string Issuer = "local-stubs";
const string Audience = "gateway-local";

var app = WebApplication.CreateBuilder(args).Build();

app.MapGet("/health", () => Results.Ok());

app.MapGet("/orders/{id}", (string id) =>
    id == "404"
        ? Results.NotFound()
        : Results.Ok(new { id, status = "Paid", total = 120.50m }));

app.MapGet("/tracking/{id}", (string id) =>
    Results.Ok(new { status = "InTransit", location = "Curitiba" }));

app.MapGet("/notifications/{id}", (string id) =>
    Results.Ok(new[] { new { text = "Order paid / Pedido pago" } }));

// EN: Mints a short-lived JWT for the "sub" given in the query string (default: user-42).
// PT: Gera um JWT de curta duração para o "sub" informado na query string (padrão: user-42).
app.MapGet("/token", (string? sub) =>
{
    var key = new SymmetricSecurityKey(Convert.FromBase64String(DevSigningKey));
    var descriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim("sub", sub ?? "user-42")]),
        Issuer = Issuer,
        Audience = Audience,
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
    };
    return Results.Text(new JsonWebTokenHandler().CreateToken(descriptor));
});

app.Run();
