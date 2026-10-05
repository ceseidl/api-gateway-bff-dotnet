var builder = WebApplication.CreateBuilder(args);
var services = builder.Configuration.GetSection("Services");

builder.Services
    .AddHttpClient<IOrderClient, OrderClient>(c =>
        c.BaseAddress = new Uri(services["Order"]!))
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<ITrackingClient, TrackingClient>(c =>
        c.BaseAddress = new Uri(services["Tracking"]!))
    .AddStandardResilienceHandler();

builder.Services
    .AddHttpClient<INotificationClient, NotificationClient>(c =>
        c.BaseAddress = new Uri(services["Notification"]!))
    .AddStandardResilienceHandler();

var app = builder.Build();
app.MapOrderEndpoints();
app.Run();
