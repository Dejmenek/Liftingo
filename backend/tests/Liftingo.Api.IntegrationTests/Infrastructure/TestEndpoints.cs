using Liftingo.Application.Common.Result;
using Liftingo.Domain.Common;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

using Serilog.Core;
using Serilog.Events;

using System.Net;
using System.Security.Claims;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

public sealed class TestDomainException(string message) : DomainException(message);

/// <summary>Collects the log events written during a test run.</summary>
public sealed class LogEventCollector : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            _events.Add(logEvent);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}

/// <summary>Maps endpoints that only exist in the test host, to exercise the error handling.</summary>
public sealed class TestEndpointsStartupFilter : IStartupFilter
{
    public const string DomainExceptionPath = "/test/domain-exception";
    public const string UnhandledExceptionPath = "/test/unhandled-exception";
    public const string ResultPath = "/test/result";
    public const string ValidationResultPath = "/test/result/validation";
    public const string GetOnlyPath = "/test/get-only";
    public const string ClientIpHeader = "X-Test-Client-Ip";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string ExceptionMessage = "Weight must be positive";
    public const string SecretMessage = "secret-internal-message";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(ClientIpHeader, out StringValues ip))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                }

                if (context.Request.Headers.TryGetValue(UserIdHeader, out StringValues userId))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        authenticationType: "Test"));
                }

                await nextMiddleware();
            });

            next(app);

            // The app maps no endpoints yet, so it doesn't add routing on its own.
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet(DomainExceptionPath, () => Throw(new TestDomainException(ExceptionMessage)));
                endpoints.MapGet(UnhandledExceptionPath, () => Throw(new InvalidOperationException(SecretMessage)));
                endpoints.MapGet(GetOnlyPath, () => Results.Ok());
                endpoints.MapGet($"{ResultPath}/{{type}}", (ErrorType type) =>
                    Result.Failure(new Error("Test.Error", "Test description", type)).ToProblem());
                endpoints.MapGet(ValidationResultPath, () => Result.Failure(ValidationError.FromErrors(
                [
                    Error.Validation("Name.Empty", "Name is required"),
                    Error.Validation("Age.Negative", "Age must not be negative"),
                ])).ToProblem());
            });
        };

    private static IResult Throw(Exception exception) => throw exception;
}

public static class TestEndpointsServiceCollectionExtensions
{
    public static IServiceCollection AddTestEndpoints(this IServiceCollection services, LogEventCollector collector)
    {
        services.AddSingleton<IStartupFilter, TestEndpointsStartupFilter>();
        services.AddSingleton<ILogEventSink>(collector);

        return services;
    }
}