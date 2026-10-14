using Serilog.Enrichers.Sensitive;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Liftingo.Api.Logging;

/// <summary>
/// Masks IPv4 and IPv6 addresses in log properties and messages. A safety net: code is not
/// meant to log addresses at all.
/// </summary>
internal sealed class IpAddressMaskingOperator : RegexMaskingOperator
{
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
        string value = match.Value;

        if (!value.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        return (value.Contains("::", StringComparison.Ordinal) || value.Count(c => c == ':') == FullIpv6ColonCount) &&
            IPAddress.TryParse(value, out IPAddress? address) &&
            address.AddressFamily == AddressFamily.InterNetworkV6;
    }
}