using System.Net;

public interface IOrderClient
{
    Task<OrderDto?> GetOrderAsync(string orderId, CancellationToken ct);
}

public interface ITrackingClient
{
    Task<TrackingDto?> GetTrackingAsync(string orderId, CancellationToken ct);
}

public interface INotificationClient
{
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(string orderId, CancellationToken ct);
}

public sealed class OrderClient(HttpClient http) : IOrderClient
{
    public async Task<OrderDto?> GetOrderAsync(string orderId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"orders/{orderId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }
}

public sealed class TrackingClient(HttpClient http) : ITrackingClient
{
    public Task<TrackingDto?> GetTrackingAsync(string orderId, CancellationToken ct) =>
        http.GetFromJsonAsync<TrackingDto>($"tracking/{orderId}", ct);
}

public sealed class NotificationClient(HttpClient http) : INotificationClient
{
    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        string orderId, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<NotificationDto>>($"notifications/{orderId}", ct) ?? [];
}
