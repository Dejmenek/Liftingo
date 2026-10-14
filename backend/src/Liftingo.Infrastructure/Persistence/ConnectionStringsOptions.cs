using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Persistence;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Default { get; init; } = string.Empty;

    public string Redis { get; init; } = string.Empty;
}

public sealed class ConnectionStringsOptionsValidator : IValidateOptions<ConnectionStringsOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Default))
        {
            failures.Add($"'{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Default)}' must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Redis))
        {
            failures.Add($"'{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Redis)}' must be configured.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}