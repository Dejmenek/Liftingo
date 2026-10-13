using Liftingo.Application.Common.Email;

namespace Liftingo.Application.Features.Account.Shared.Emails;

/// <summary>Welcome without a link, for an account whose e-mail the provider vouched for (FR-025).</summary>
public sealed record WelcomeEmail(Guid UserId, string To) : OutgoingEmail(UserId, To);
