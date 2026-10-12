using Liftingo.Application.Common.Email;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

/// <summary>Replaces <see cref="IEmailSender"/> in tests: remembers every message instead of sending it.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _messages = [];
    private readonly Lock _lock = new();

    /// <summary>When set, <see cref="SendAsync"/> throws after capturing nothing, to test FR-029.</summary>
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
            throw new InvalidOperationException("The test e-mail sender is set to fail.");
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
