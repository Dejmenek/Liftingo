namespace Liftingo.Application.Common.Email;

public interface IEmailSender
{
    /// <summary>
    /// Renders and sends the e-mail. Call it only after the data the e-mail refers to has been saved. It never
    /// throws, except when <paramref name="ct"/> is cancelled: a failed send is logged with the e-mail type and
    /// user id and does not fail the use case.
    /// </summary>
    Task SendAsync(OutgoingEmail email, CancellationToken ct);
}
