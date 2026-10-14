using Asp.Versioning;

using Liftingo.Api.Extensions;
using Liftingo.Application;
using Liftingo.Infrastructure;

using Serilog;

Log.Logger = SerilogExtensions.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();

    builder.Services.AddApiVersioningWithOpenApi();

    builder.Services.AddForwardedHeaderSupport(builder.Configuration);

    builder.Services.AddExceptionHandling(builder.Environment);

    builder.Services.AddRateLimiting(builder.Configuration);

    builder.Services.AddRequestTimeoutPolicies(builder.Configuration);

    builder.Services.AddApplication();

    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    builder.Services.AddAuthenticationSettings();

    var app = builder.Build();

    app.UseForwardedHeaderSupport();

    app.UseExceptionHandling();

    app.UseSerilogLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().WithDocumentPerVersion();

        app.MapDevPages();

        await app.ApplyMigrationsAsync();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Authentication runs before the rate limiter so signed-in calls get the per-user bucket.
    app.UseAuthentication();

    app.UseRateLimiting();

    app.UseAuthorization();

    app.UseRequestTimeoutPolicies();

    app.MapVersionedApi();

    await app.RunAsync();

    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");

    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}