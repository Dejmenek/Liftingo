using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using Serilog.Enrichers.Sensitive;

namespace Liftingo.Api.Logging;

/// <summary>
/// Masks IPv4 and IPv6 addresses in log properties and messages. A safety net: code is not
/// meant to log addresses at all.
/// </summary>
internal sealed class IpAddressMaskingOperator : RegexMaskingOperator
{
    // IPv4 as four dotted groups, or a run of hex groups and colons that the filter below checks.
    // The lookarounds keep it from cutting an address out of a longer hex or colon run.
    private const string Pattern =
        @"(?<![0-9a-z.])(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?![0-9a-z.])" +
        @"|(?<![0-9a-f:])(?:[0-9a-f]{0,4}:){2,7}[0-9a-f]{0,4}(?![0-9a-f:])";

    private const int FullIpv6ColonCount = 7;

    public IpAddressMaskingOperator()
        : base(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)
    {
    }

    protected override bool ShouldMaskMatch(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);

        string value = match.Value;

        if (!value.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        // A time such as 10:15:00 fits the pattern above but is not an address: IPv6 needs "::" or eight groups.
        return (value.Contains("::", StringComparison.Ordinal) || value.Count(c => c == ':') == FullIpv6ColonCount) &&
            IPAddress.TryParse(value, out IPAddress? address) &&
            address.AddressFamily == AddressFamily.InterNetworkV6;
    }
}
