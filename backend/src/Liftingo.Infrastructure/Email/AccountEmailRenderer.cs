using System.Globalization;
using System.Net;

using Liftingo.Application.Common.Configuration;
using Liftingo.Application.Common.Email;
using Liftingo.Application.Features.Account.Shared.Emails;

using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Email;

/// <summary>
/// The Polish e-mails of the Account module, built from plain strings (no templating engine). Links point at the
/// frontend, and times are shown in <see cref="EmailOptions.DisplayTimeZone"/> (R24, FR-026a).
/// </summary>
public sealed class AccountEmailRenderer(IOptions<AppOptions> appOptions, IOptions<EmailOptions> emailOptions)
{
    public const string VerificationAndWelcomeTemplate = "VerificationAndWelcome";
    public const string VerificationTemplate = "Verification";
    public const string PasswordResetTemplate = "PasswordReset";
    public const string ProviderLinkConfirmationTemplate = "ProviderLinkConfirmation";
    public const string WelcomeTemplate = "Welcome";
    public const string NewDeviceTemplate = "NewDevice";

    // Validated at startup (EmailOptionsValidator), so the lookup cannot fail here.
    private readonly TimeZoneInfo _displayZone = TimeZoneInfo.FindSystemTimeZoneById(emailOptions.Value.DisplayTimeZone);

    // FrontendBaseUrl is validated at startup (absolute https URL).
    private string BaseUrl => appOptions.Value.FrontendBaseUrl!.AbsoluteUri.TrimEnd('/');

    /// <returns>The template name of a record, or its type name when this renderer does not know it.</returns>
    public static string TemplateNameOf(OutgoingEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email switch
        {
            VerificationAndWelcomeEmail => VerificationAndWelcomeTemplate,
            VerificationEmail => VerificationTemplate,
            PasswordResetEmail => PasswordResetTemplate,
            ProviderLinkConfirmationEmail => ProviderLinkConfirmationTemplate,
            WelcomeEmail => WelcomeTemplate,
            NewDeviceEmail => NewDeviceTemplate,
            _ => email.GetType().Name,
        };
    }

    public EmailMessage Render(OutgoingEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email switch
        {
            VerificationAndWelcomeEmail e => VerificationAndWelcome(e),
            VerificationEmail e => Verification(e),
            PasswordResetEmail e => PasswordReset(e),
            ProviderLinkConfirmationEmail e => ProviderLinkConfirmation(e),
            WelcomeEmail e => Welcome(e),
            NewDeviceEmail e => NewDevice(e),
            _ => throw new ArgumentException($"No template for {email.GetType().Name}.", nameof(email)),
        };
    }

    // Verification link doubling as the welcome e-mail (FR-025).
    private EmailMessage VerificationAndWelcome(VerificationAndWelcomeEmail email)
    {
        string link = Link("auth/verify-email", email.Token);

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

        return new EmailMessage(email.To, subject, html, text, VerificationAndWelcomeTemplate, email.UserId);
    }

    // Verification link without the welcome text, for a resend (FR-018).
    private EmailMessage Verification(VerificationEmail email)
    {
        string link = Link("auth/verify-email", email.Token);

        const string subject = "Potwierdź swój adres e-mail w Liftingo";

        string text =
            "Aby potwierdzić adres e-mail, otwórz poniższy link (ważny 24 godziny):\n" +
            $"{link}\n\n" +
            "Jeśli to nie Ty prosiłeś(-aś) o potwierdzenie, zignoruj tę wiadomość.";

        string html =
            "<p>Aby potwierdzić adres e-mail, kliknij poniższy link (ważny 24 godziny):</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Potwierdź adres e-mail</a></p>" +
            "<p>Jeśli to nie Ty prosiłeś(-aś) o potwierdzenie, zignoruj tę wiadomość.</p>";

        return new EmailMessage(email.To, subject, html, text, VerificationTemplate, email.UserId);
    }

    // Password reset link, valid one hour (FR-033).
    private EmailMessage PasswordReset(PasswordResetEmail email)
    {
        string link = Link("auth/reset-password", email.Token);

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

        return new EmailMessage(email.To, subject, html, text, PasswordResetTemplate, email.UserId);
    }

    // Asks the owner of an address to confirm that a sign-in provider may be linked to the account (FR-023a, R17).
    private EmailMessage ProviderLinkConfirmation(ProviderLinkConfirmationEmail email)
    {
        string link = Link("auth/confirm-link", email.Token);

        const string subject = "Potwierdź połączenie logowania z kontem Liftingo";

        string text =
            $"Ktoś chce zalogować się do Liftingo przez {email.Provider} i podał Twój adres e-mail.\n\n" +
            "Jeśli to Ty, otwórz poniższy link, aby połączyć to logowanie z Twoim kontem (ważny 24 godziny):\n" +
            $"{link}\n\n" +
            "Jeśli to nie Ty, zignoruj tę wiadomość. Twoje konto pozostanie bez zmian.";

        string html =
            $"<p>Ktoś chce zalogować się do Liftingo przez {WebUtility.HtmlEncode(email.Provider)} i podał Twój adres e-mail.</p>" +
            "<p>Jeśli to Ty, kliknij poniższy link, aby połączyć to logowanie z Twoim kontem (ważny 24 godziny):</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Połącz logowanie z kontem</a></p>" +
            "<p>Jeśli to nie Ty, zignoruj tę wiadomość. Twoje konto pozostanie bez zmian.</p>";

        return new EmailMessage(email.To, subject, html, text, ProviderLinkConfirmationTemplate, email.UserId);
    }

    // Welcome without a link, for an account whose e-mail the provider already vouched for (FR-025).
    private static EmailMessage Welcome(WelcomeEmail email)
    {
        const string subject = "Witaj w Liftingo";

        const string text =
            "Witaj w Liftingo!\n\n" +
            "Twoje konto jest gotowe, a adres e-mail został potwierdzony przez dostawcę logowania. Możesz zaczynać trening.";

        const string html =
            "<p>Witaj w Liftingo!</p>" +
            "<p>Twoje konto jest gotowe, a adres e-mail został potwierdzony przez dostawcę logowania. Możesz zaczynać trening.</p>";

        return new EmailMessage(email.To, subject, html, text, WelcomeTemplate, email.UserId);
    }

    // Security notice for a sign-in from a device the account has not used before (FR-026), in the display zone.
    private EmailMessage NewDevice(NewDeviceEmail email)
    {
        string when = TimeZoneInfo
            .ConvertTime(email.SignedInAt, _displayZone)
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        string signOutAll = PlainLink("account/sessions");
        string resetPassword = PlainLink("auth/forgot-password");

        const string subject = "Nowe logowanie do Twojego konta w Liftingo";

        string text =
            "Zalogowano się do Twojego konta w Liftingo z nowego urządzenia.\n\n" +
            $"Czas logowania: {when}\n" +
            $"Urządzenie: {email.DeviceType}\n\n" +
            "Jeśli to Ty, nie musisz nic robić. Jeśli to nie Ty, wyloguj się ze wszystkich urządzeń:\n" +
            $"{signOutAll}\n\n" +
            "i zresetuj hasło:\n" +
            $"{resetPassword}";

        string html =
            "<p>Zalogowano się do Twojego konta w Liftingo z nowego urządzenia.</p>" +
            $"<p>Czas logowania: {WebUtility.HtmlEncode(when)}<br>Urządzenie: {WebUtility.HtmlEncode(email.DeviceType)}</p>" +
            "<p>Jeśli to Ty, nie musisz nic robić. Jeśli to nie Ty:</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(signOutAll)}\">Wyloguj się ze wszystkich urządzeń</a></p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(resetPassword)}\">Zresetuj hasło</a></p>";

        return new EmailMessage(email.To, subject, html, text, NewDeviceTemplate, email.UserId);
    }

    private string PlainLink(string path) => string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}/{path}");

    private string Link(string path, string token) =>
        string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}/{path}?token={Uri.EscapeDataString(token)}");
}
