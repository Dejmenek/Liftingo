using Asp.Versioning;
using Asp.Versioning.Builder;

using Liftingo.Api.Endpoints;

namespace Liftingo.Api.Extensions;

internal static class ApiVersioningExtensions
{
    private const string VersionedRoutePrefix = "/api/v{version:apiVersion}";
    private const string GroupNameFormat = "'v'VVV";

    public static IServiceCollection AddApiVersioningWithOpenApi(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = V1Endpoints.Version;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.ReportApiVersions = true;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = GroupNameFormat;
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi();

        return services;
    }

    public static WebApplication MapVersionedApi(this WebApplication app)
    {
        IVersionedEndpointRouteBuilder versionedApi = app.NewVersionedApi();

        versionedApi
            .MapGroup(VersionedRoutePrefix)
            .HasApiVersion(V1Endpoints.Version)
            .MapV1Endpoints();

        return app;
    }
}