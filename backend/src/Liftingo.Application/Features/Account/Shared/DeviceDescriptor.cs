using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// Turns a User-Agent header into a coarse, human-readable device label, e.g. "Chrome, Windows, komputer".
/// A hand-written classifier is enough for one label; a UA-parser package would be a dependency for nothing.
/// </summary>
public static class DeviceDescriptor
{
    private const string UnknownBrowser = "inna przeglądarka";
    private const string UnknownOs = "inny system";

    // The first rule with a matching fragment wins. Order matters: Edge and Opera also say "Chrome/", and
    // Chrome says "Safari/".
    private static readonly (string Label, string[] Fragments)[] s_browsers =
    [
        ("Edge", ["Edg/", "EdgA/", "EdgiOS/"]),
        ("Opera", ["OPR/", "Opera"]),
        ("Firefox", ["Firefox/", "FxiOS/"]),
        ("Chrome", ["Chrome/", "CriOS/"]),
        ("Safari", ["Safari/"]),
    ];

    // iOS user agents contain "like Mac OS X" and Android ones contain "Linux", so those go first.
    private static readonly (string Label, string[] Fragments)[] s_operatingSystems =
    [
        ("iOS", ["iPhone", "iPad", "iPod"]),
        ("Android", ["Android"]),
        ("Windows", ["Windows"]),
        ("macOS", ["Macintosh", "Mac OS X"]),
        ("Linux", ["Linux", "X11"]),
    ];

    public static string Describe(string? userAgent)
    {
        string ua = userAgent ?? string.Empty;

        string label =
            $"{Match(ua, s_browsers, UnknownBrowser)}, {Match(ua, s_operatingSystems, UnknownOs)}, {FormFactor(ua)}";

        return label.Length > KnownDevice.DeviceTypeMaxLength ? label[..KnownDevice.DeviceTypeMaxLength] : label;
    }

    // Android tablets omit "Mobile" from the user agent; phones include it.
    private static string FormFactor(string ua)
    {
        if (Contains(ua, "iPad") || Contains(ua, "Tablet") || (Contains(ua, "Android") && !Contains(ua, "Mobile")))
        {
            return "tablet";
        }

        return Contains(ua, "iPhone") || Contains(ua, "iPod") || Contains(ua, "Mobile") ? "telefon" : "komputer";
    }

    private static string Match(string ua, (string Label, string[] Fragments)[] rules, string fallback)
    {
        foreach ((string label, string[] fragments) in rules)
        {
            if (fragments.Any(fragment => Contains(ua, fragment)))
            {
                return label;
            }
        }

        return fallback;
    }

    private static bool Contains(string ua, string fragment) =>
        ua.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
