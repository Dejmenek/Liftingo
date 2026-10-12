namespace Liftingo.Domain.Account;

public sealed class KnownDevice
{
    public const int DeviceTypeMaxLength = 100;

    // For EF Core.
    private KnownDevice()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string DeviceType { get; private set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public static KnownDevice Create(Guid userId, Guid deviceId, string deviceType, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            DeviceType = Truncate(deviceType),
            FirstSeenAt = now,
            LastSeenAt = now,
        };

    public void RecordSignIn(string deviceType, DateTimeOffset now)
    {
        DeviceType = Truncate(deviceType);
        LastSeenAt = now;
    }

    private static string Truncate(string deviceType) =>
        deviceType.Length > DeviceTypeMaxLength ? deviceType[..DeviceTypeMaxLength] : deviceType;
}
