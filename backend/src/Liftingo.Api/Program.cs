using Liftingo.Api.Extensions;
using Liftingo.Infrastructure;

using Serilog;

Log.Logger = SerilogExtensions.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    builder.Services.AddExceptionHandling(builder.Environment);

    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandling();

    app.UseSerilogLogging();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        await app.ApplyMigrationsAsync();
    }

    app.UseHttpsRedirection();

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