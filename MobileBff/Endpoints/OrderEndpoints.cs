public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/order/{orderId}", GetOrderDetails);
        return app;
    }

    private static async Task<IResult> GetOrderDetails(
        string orderId,
        IOrderClient orders,
        ITrackingClient tracking,
        INotificationClient notifications,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var orderTask = orders.GetOrderAsync(orderId, ct);
        var trackingTask = tracking.GetTrackingAsync(orderId, ct);
        var notificationsTask =
            notifications.GetNotificationsAsync(orderId, ct);

        var order = await orderTask;
        if (order is null)
            return Results.NotFound();

        // EN: Secondary data: a partial failure does not break the screen.
        // PT: Dados secundários: falha parcial não derruba a tela.
        var trackingResult = await SafeAsync(
            trackingTask, logger, "tracking");
        var notificationsResult = await SafeAsync(
            notificationsTask, logger, "notifications")
            ?? Array.Empty<NotificationDto>();

        return Results.Ok(new OrderDetailsResponse(
            order, trackingResult, notificationsResult));
    }

    private static async Task<T?> SafeAsync<T>(
        Task<T> task, ILogger logger, string source)
    {
        try { return await task; }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex,
                "Partial failure querying / Falha parcial ao consultar {Source}", source);
            return default;
        }
    }
}
