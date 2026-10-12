using Liftingo.Domain.Account;

using Microsoft.Extensions.Time.Testing;

namespace Liftingo.Api.UnitTests.Domain.Account;

public class KnownDeviceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_ValidInput_SetsBothTimestampsToNow()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        KnownDevice device = KnownDevice.Create(userId, deviceId, "Chrome, Windows, komputer", _time.GetUtcNow());

        Assert.NotEqual(Guid.Empty, device.Id);
        Assert.Equal(userId, device.UserId);
        Assert.Equal(deviceId, device.DeviceId);
        Assert.Equal("Chrome, Windows, komputer", device.DeviceType);
        Assert.Equal(_time.GetUtcNow(), device.FirstSeenAt);
        Assert.Equal(_time.GetUtcNow(), device.LastSeenAt);
    }

    [Fact]
    public void Create_DeviceTypeLongerThan100Chars_TruncatesTo100()
    {
        KnownDevice device = KnownDevice.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', 150), _time.GetUtcNow());

        Assert.Equal(100, device.DeviceType.Length);
    }

    [Fact]
    public void RecordSignIn_KnownDevice_UpdatesDeviceTypeAndLastSeenOnly()
    {
        KnownDevice device = KnownDevice.Create(Guid.NewGuid(), Guid.NewGuid(), "Chrome, Windows, komputer", _time.GetUtcNow());
        DateTimeOffset firstSeen = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromDays(3));

        device.RecordSignIn("Firefox, Windows, komputer", _time.GetUtcNow());

        Assert.Equal("Firefox, Windows, komputer", device.DeviceType);
        Assert.Equal(_time.GetUtcNow(), device.LastSeenAt);
        Assert.Equal(firstSeen, device.FirstSeenAt);
    }

    [Fact]
    public void RecordSignIn_DeviceTypeLongerThan100Chars_TruncatesTo100()
    {
        KnownDevice device = KnownDevice.Create(Guid.NewGuid(), Guid.NewGuid(), "Chrome", _time.GetUtcNow());

        device.RecordSignIn(new string('y', 101), _time.GetUtcNow());

        Assert.Equal(100, device.DeviceType.Length);
    }
}
