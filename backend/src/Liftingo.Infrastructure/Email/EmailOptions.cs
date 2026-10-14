namespace Liftingo.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// The IANA time zone that times in e-mails are shown in, with daylight saving applied. An id the
    /// host does not know stops the app at startup.
    /// </summary>
    public string DisplayTimeZone { get; init; } = "Europe/Warsaw";
}