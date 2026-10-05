public sealed record OrderDetailsResponse(
    OrderDto Order,
    TrackingDto? Tracking,
    IReadOnlyList<NotificationDto> Notifications);
