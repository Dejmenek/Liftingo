namespace Liftingo.Api.Endpoints.Account;

/// <summary>The <c>202</c> body of <c>POST /auth/external/{provider}/login</c> when a link-confirmation e-mail went out.</summary>
/// <param name="Status">Always <see cref="LinkConfirmationSent"/>.</param>
public sealed record ExternalLoginLinkConfirmationResponse(string Status)
{
    public const string LinkConfirmationSent = "linkConfirmationSent";
}