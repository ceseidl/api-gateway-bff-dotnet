var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IOrderClient, OrderClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Services:Order"]!))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<ITrackingClient, TrackingClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Services:Tracking"]!))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<INotificationClient, NotificationClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Services:Notification"]!))
    .AddStandardResilienceHandler();

var app = builder.Build();
app.MapOrderEndpoints();
app.Run();
