using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Features/Account/Shared is the agreed folder for helpers shared by the Account slices.",
    Scope = "namespace",
    Target = "~N:Liftingo.Application.Features.Account.Shared")]

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Features/Account/Shared/Emails holds the e-mail records shared by the Account slices.",
    Scope = "namespace",
    Target = "~N:Liftingo.Application.Features.Account.Shared.Emails")]
