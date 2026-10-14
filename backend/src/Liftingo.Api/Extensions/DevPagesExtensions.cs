using Liftingo.Api.Endpoints;
using System.Text;

namespace Liftingo.Api.Extensions;

internal static class DevPagesExtensions
{
    private const string ProviderSignInResource = "DevPages.provider-sign-in.html";

    private static readonly Lazy<string> s_providerSignIn = new(() => ReadPage(ProviderSignInResource));

    /// <summary>
    /// Pages for trying by hand what has no frontend yet, e.g. a real Google or Facebook sign-in. Call only in
    /// Development: they are not part of the API and are left out of the OpenAPI document.
    /// </summary>
    public static WebApplication MapDevPages(this WebApplication app)
    {
        app.MapGet(RouteConsts.Dev.ProviderSignIn, () => Results.Content(s_providerSignIn.Value, "text/html", Encoding.UTF8))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private static string ReadPage(string resourceName)
    {
        using Stream stream = typeof(DevPagesExtensions).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded page '{resourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}