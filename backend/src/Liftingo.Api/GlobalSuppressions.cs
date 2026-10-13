using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Endpoints/Account/Shared is the agreed folder for HTTP helpers shared by the Account endpoints.",
    Scope = "namespace",
    Target = "~N:Liftingo.Api.Endpoints.Account.Shared")]
