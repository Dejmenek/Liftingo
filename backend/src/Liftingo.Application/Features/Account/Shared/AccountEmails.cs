using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

using Liftingo.Application.Common.Email;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// The Polish e-mails of the Account module, built from plain strings (no templating engine) and sent
/// through <see cref="IEmailSender"/>. A failed send is logged with the template name and user id only
/// and never fails the flow that triggered it (FR-029).
/// </summary>
public sealed partial class AccountEmails(
    IEmailSender sender,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider,
    ILogger<AccountEmails> logger)
{
    public const string WelcomeTemplate = "Welcome";
    public const string NewDeviceTemplate = "NewDevice";

    private static readonly TimeZoneInfo s_displayZone = FindDisplayZone();

    public const string VerificationAndWelcomeTemplate = "VerificationAndWelcome";
    public const string VerificationTemplate = "Verification";
    public const string PasswordResetTemplate = "PasswordReset";
    public const string ProviderLinkConfirmationTemplate = "ProviderLinkConfirmation";

    /// <summary>Verification link doubling as the welcome e-mail (FR-025).</summary>
    public async Task SendVerificationAndWelcomeAsync(Guid userId, string email, string token, CancellationToken ct)
    {
        string link = Link("auth/verify-email", token);

        const string subject = "Witaj w Liftingo – potwierdź swój adres e-mail";

        string text =
            "Witaj w Liftingo!\n\n" +
            "Twoje konto jest gotowe. Aby potwierdzić adres e-mail, otwórz poniższy link (ważny 24 godziny):\n" +
            $"{link}\n\n" +
            "Jeśli to nie Ty zakładasz konto, zignoruj tę wiadomość.";

        string html =
            "<p>Witaj w Liftingo!</p>" +
            "<p>Twoje konto jest gotowe. Aby potwierdzić adres e-mail, kliknij poniższy link (ważny 24 godziny):</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Potwierdź adres e-mail</a></p>" +
            "<p>Jeśli to nie Ty zakładasz konto, zignoruj tę wiadomość.</p>";

        await SendSafelyAsync(new EmailMessage(email, subject, html, text, VerificationAndWelcomeTemplate, userId), ct);
    }

    /// <summary>Verification link without the welcome text, for a resend (FR-018).</summary>
    public async Task SendVerificationAsync(Guid userId, string email, string token, CancellationToken ct)
    {
        string link = Link("auth/verify-email", token);

        const string subject = "Potwierdź swój adres e-mail w Liftingo";

        string text =
            "Aby potwierdzić adres e-mail, otwórz poniższy link (ważny 24 godziny):\n" +
            $"{link}\n\n" +
            "Jeśli to nie Ty prosiłeś(-aś) o potwierdzenie, zignoruj tę wiadomość.";

        string html =
            "<p>Aby potwierdzić adres e-mail, kliknij poniższy link (ważny 24 godziny):</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Potwierdź adres e-mail</a></p>" +
            "<p>Jeśli to nie Ty prosiłeś(-aś) o potwierdzenie, zignoruj tę wiadomość.</p>";

        await SendSafelyAsync(new EmailMessage(email, subject, html, text, VerificationTemplate, userId), ct);
    }

    /// <summary>Password reset link, valid one hour (FR-032).</summary>
    public async Task SendPasswordResetAsync(Guid userId, string email, string token, CancellationToken ct)
    {
        string link = Link("auth/reset-password", token);

        const string subject = "Reset hasła w Liftingo";

        string text =
            "Otrzymaliśmy prośbę o zresetowanie hasła do Twojego konta w Liftingo.\n\n" +
            "Aby ustawić nowe hasło, otwórz poniższy link (ważny 1 godzinę):\n" +
            $"{link}\n\n" +
            "Jeśli to nie Ty prosiłeś(-aś) o reset hasła, zignoruj tę wiadomość. Twoje hasło pozostanie bez zmian.";

        string html =
            "<p>Otrzymaliśmy prośbę o zresetowanie hasła do Twojego konta w Liftingo.</p>" +
            "<p>Aby ustawić nowe hasło, kliknij poniższy link (ważny 1 godzinę):</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Ustaw nowe hasło</a></p>" +
            "<p>Jeśli to nie Ty prosiłeś(-aś) o reset hasła, zignoruj tę wiadomość. Twoje hasło pozostanie bez zmian.</p>";

        await SendSafelyAsync(new EmailMessage(email, subject, html, text, PasswordResetTemplate, userId), ct);
    }

    /// <summary>
    /// Asks the owner of an address to confirm that a sign-in provider may be linked to the account (FR-023a, R17).
    /// </summary>
    public async Task SendProviderLinkConfirmationAsync(Guid userId, string email, string provider, string token, CancellationToken ct)
    {
        string link = Link("auth/confirm-link", token);

        const string subject = "Potwierdź połączenie logowania z kontem Liftingo";

        string text =
            $"Ktoś chce zalogować się do Liftingo przez {provider} i podał Twój adres e-mail.\n\n" +
            "Jeśli to Ty, otwórz poniższy link, aby połączyć to logowanie z Twoim kontem (ważny 24 godziny):\n" +
            $"{link}\n\n" +
            "Jeśli to nie Ty, zignoruj tę wiadomość. Twoje konto pozostanie bez zmian.";

        string html =
            $"<p>Ktoś chce zalogować się do Liftingo przez {WebUtility.HtmlEncode(provider)} i podał Twój adres e-mail.</p>" +
            "<p>Jeśli to Ty, kliknij poniższy link, aby połączyć to logowanie z Twoim kontem (ważny 24 godziny):</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Połącz logowanie z kontem</a></p>" +
            "<p>Jeśli to nie Ty, zignoruj tę wiadomość. Twoje konto pozostanie bez zmian.</p>";

        await SendSafelyAsync(new EmailMessage(email, subject, html, text, ProviderLinkConfirmationTemplate, userId), ct);
    }

    /// <summary>Welcome without a link, for an account whose e-mail the provider already vouched for (FR-025).</summary>
    public async Task SendWelcomeAsync(Guid userId, string email, CancellationToken ct)
    {
        const string subject = "Witaj w Liftingo";

        const string text =
            "Witaj w Liftingo!\n\n" +
            "Twoje konto jest gotowe, a adres e-mail został potwierdzony przez dostawcę logowania. Możesz zaczynać trening.";

        const string html =
            "<p>Witaj w Liftingo!</p>" +
            "<p>Twoje konto jest gotowe, a adres e-mail został potwierdzony przez dostawcę logowania. Możesz zaczynać trening.</p>";

        await SendSafelyAsync(new EmailMessage(email, subject, html, text, WelcomeTemplate, userId), ct);
    }

    /// <summary>
    /// Security notice for a successful sign-in from a device the account has not used before (FR-026). The time is
    /// shown in Polish time.
    /// </summary>
    public async Task SendNewDeviceAsync(Guid userId, string email, string deviceType, CancellationToken ct)
    {
        string when = TimeZoneInfo
            .ConvertTime(timeProvider.GetUtcNow(), s_displayZone)
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        string signOutAll = PlainLink("account/sessions");
        string resetPassword = PlainLink("auth/forgot-password");

        const string subject = "Nowe logowanie do Twojego konta w Liftingo";

        string text =
            $"Zalogowano się do Twojego konta w Liftingo z nowego urządzenia.\n\n" +
            $"Czas logowania: {when}\n" +
            $"Urządzenie: {deviceType}\n\n" +
            "Jeśli to Ty, nie musisz nic robić. Jeśli to nie Ty, wyloguj się ze wszystkich urządzeń:\n" +
            $"{signOutAll}\n\n" +
            "i zresetuj hasło:\n" +
            $"{resetPassword}";

        string html =
            "<p>Zalogowano się do Twojego konta w Liftingo z nowego urządzenia.</p>" +
            $"<p>Czas logowania: {WebUtility.HtmlEncode(when)}<br>Urządzenie: {WebUtility.HtmlEncode(deviceType)}</p>" +
            "<p>Jeśli to Ty, nie musisz nic robić. Jeśli to nie Ty:</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(signOutAll)}\">Wyloguj się ze wszystkich urządzeń</a></p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(resetPassword)}\">Zresetuj hasło</a></p>";

        await SendSafelyAsync(new EmailMessage(email, subject, html, text, NewDeviceTemplate, userId), ct);
    }

    private string PlainLink(string path) =>
        string.Create(CultureInfo.InvariantCulture, $"{appOptions.Value.FrontendBaseUrl!.AbsoluteUri.TrimEnd('/')}/{path}");

    private static TimeZoneInfo FindDisplayZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private string Link(string path, string token)
    {
        // FrontendBaseUrl is validated at startup (absolute https URL).
        string baseUrl = appOptions.Value.FrontendBaseUrl!.AbsoluteUri.TrimEnd('/');

        return string.Create(CultureInfo.InvariantCulture, $"{baseUrl}/{path}?token={Uri.EscapeDataString(token)}");
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed e-mail must never fail the flow that triggered it (FR-029); the failure is logged.")]
    private async Task SendSafelyAsync(EmailMessage message, CancellationToken ct)
    {
        try
        {
            await sender.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Template name and user id only: the exception is not logged, since a sender may put the recipient in it.
            LogSendFailed(ex.GetType().Name, message.TemplateName, message.UserId);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Sending the {TemplateName} e-mail for user {UserId} failed ({ExceptionType}).")]
    private partial void LogSendFailed(string exceptionType, string templateName, Guid userId);
}
