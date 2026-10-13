using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Test folders mirror the source, and Features/Account/Shared is the agreed source folder.",
    Scope = "namespace",
    Target = "~N:Liftingo.Api.UnitTests.Features.Account.Shared")]
