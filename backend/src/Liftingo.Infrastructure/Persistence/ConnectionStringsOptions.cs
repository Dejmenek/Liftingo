using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Persistence;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Default { get; init; } = string.Empty;
}

public sealed class ConnectionStringsOptionsValidator : IValidateOptions<ConnectionStringsOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Default))
        {
            return ValidateOptionsResult.Fail(
                $"'{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Default)}' must be configured.");
        }

        return ValidateOptionsResult.Success;
    }
}