using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

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
        // EN: Token for the secondary calls: cancelled when the order is
        // EN: not found or fails, so no work is left running in the background.
        // PT: Token das chamadas secundárias: cancelado quando o pedido
        // PT: não existe ou falha, para não deixar trabalho rodando por trás.
        using var secondary =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        var orderTask = orders.GetOrderAsync(orderId, ct);

        // EN: SafeAsync never throws for expected failures, so these tasks
        // EN: are always observed, even if we return before awaiting them.
        // PT: O SafeAsync não lança nas falhas esperadas, então essas tasks
        // PT: são sempre observadas, mesmo se retornarmos antes de aguardá-las.
        var trackingTask = SafeAsync(
            () => tracking.GetTrackingAsync(orderId, secondary.Token),
            logger, "tracking", secondary.Token);
        var notificationsTask = SafeAsync(
            () => notifications.GetNotificationsAsync(
                orderId, secondary.Token),
            logger, "notifications", secondary.Token);

        OrderDto? order;
        try
        {
            order = await orderTask;
        }
        catch
        {
            await secondary.CancelAsync();
            throw;
        }

        if (order is null)
        {
            await secondary.CancelAsync();
            return Results.NotFound();
        }

        // EN: Secondary data: a partial failure does not break the screen.
        // PT: Dados secundários: falha parcial não derruba a tela.
        var trackingResult = await trackingTask;
        var notificationsResult = await notificationsTask
            ?? Array.Empty<NotificationDto>();

        return Results.Ok(new OrderDetailsResponse(
            order, trackingResult, notificationsResult));
    }

    private static async Task<T?> SafeAsync<T>(
        Func<Task<T>> call, ILogger logger, string source,
        CancellationToken token)
    {
        try { return await call(); }
        // EN: Cancelled on purpose (order not found) or caller gone: no warning.
        // PT: Cancelado de propósito (pedido não existe) ou chamador saiu: sem aviso.
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return default;
        }
        catch (Exception ex) when (
            // EN: Includes what the resilience handler throws: timeout,
            // EN: open circuit and rejection by the rate limiter.
            // PT: Inclui o que o resilience handler lança: timeout,
            // PT: circuito aberto e rejeição pelo rate limiter.
            ex is HttpRequestException
                or OperationCanceledException
                or TimeoutRejectedException
                or BrokenCircuitException
                or RateLimiterRejectedException)
        {
            logger.LogWarning(ex,
                "Partial failure querying / Falha parcial ao consultar {Source}", source);
            return default;
        }
    }
}
