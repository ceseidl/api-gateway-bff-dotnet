public sealed record OrderDto(
    string Id, string Status, decimal Total);

public sealed record TrackingDto(string Status, string Location);

public sealed record NotificationDto(string Text);
