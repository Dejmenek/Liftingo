using Liftingo.Infrastructure.Email;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces <see cref="IEmailTransport"/> in tests: remembers every rendered message instead of sending it, so tests
/// see the subject, body and links the real renderer produced.
/// </summary>
public sealed class CapturingEmailTransport : IEmailTransport
{
    private readonly List<EmailMessage> _messages = [];
    private readonly Lock _lock = new();

    /// <summary>When set, <see cref="SendAsync"/> throws after capturing nothing, to test that a failed send does not fail the flow.</summary>
    public bool ThrowOnSend { get; set; }

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _messages];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("The test e-mail transport is set to fail.");
        }

        lock (_lock)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }

        ThrowOnSend = false;
    }
}