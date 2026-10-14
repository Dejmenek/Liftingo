using Liftingo.Api.Http;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Liftingo.Api.UnitTests.Http;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.TooManyRequests, StatusCodes.Status429TooManyRequests)]
    public void ToProblem_FailureOfEachErrorType_MapsToExpectedStatusCode(ErrorType type, int expectedStatusCode)
    {
        Application.Common.Result.Result result = Application.Common.Result.Result.Failure(
            new Error("Some.Code", "Some description", type));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.ToProblem());

        Assert.Equal(expectedStatusCode, problem.StatusCode);
        Assert.Equal(expectedStatusCode, problem.ProblemDetails.Status);
    }

    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.TooManyRequests)]
    public void ToProblem_NonValidationFailure_ExposesCodeAndDescriptionAsDetail(ErrorType type)
    {
        Application.Common.Result.Result result = Application.Common.Result.Result.Failure(
            new Error("Plan.Missing", "The plan does not exist", type));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.ToProblem());

        Assert.Equal("The plan does not exist", problem.ProblemDetails.Detail);
        Assert.Equal("Plan.Missing", problem.ProblemDetails.Extensions[ResultExtensions.CodeExtension]);
        Assert.DoesNotContain(ResultExtensions.ErrorsExtension, problem.ProblemDetails.Extensions.Keys);
    }

    [Fact]
    public void ToProblem_ValidationErrorWithMultipleErrors_ExposesAllOfThemInOrder()
    {
        ValidationError validationError = ValidationError.FromErrors(
        [
            Error.Validation("Name.Empty", "Name is required"),
            Error.Validation("Age.Negative", "Age must not be negative"),
        ]);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(
            Application.Common.Result.Result.Failure(validationError).ToProblem());

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("Validation.General", problem.ProblemDetails.Extensions[ResultExtensions.CodeExtension]);

        IEnumerable<ErrorDetail> errors = Assert.IsAssignableFrom<IEnumerable<ErrorDetail>>(
            problem.ProblemDetails.Extensions[ResultExtensions.ErrorsExtension]);
        Assert.Equal(
            [new ErrorDetail("Name.Empty", "Name is required"), new ErrorDetail("Age.Negative", "Age must not be negative")],
            errors);
    }

    [Fact]
    public void ToProblem_LockedOutError_Returns429WithRetryAfterExtension()
    {
        var retryAfter = new DateTimeOffset(2026, 10, 1, 12, 15, 0, TimeSpan.Zero);

        RetryAfterProblemResult problem = Assert.IsType<RetryAfterProblemResult>(
            Application.Common.Result.Result.Failure(new LockedOutError(retryAfter)).ToProblem());

        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.StatusCode);
        Assert.Equal(LockedOutError.ErrorCode, problem.Problem.ProblemDetails.Extensions[ResultExtensions.CodeExtension]);
        Assert.Equal("2026-10-01T12:15:00.0000000+00:00", problem.Problem.ProblemDetails.Extensions[ResultExtensions.RetryAfterExtension]);
    }

    [Fact]
    public async Task ExecuteAsync_LockedOutError_SendsRetryAfterHeaderInSecondsFromRequestTimeProvider()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        IResult problem = Application.Common.Result.Result
            .Failure(new LockedOutError(time.GetUtcNow().AddMinutes(15).AddMilliseconds(200)))
            .ToProblem();
        DefaultHttpContext httpContext = CreateHttpContext(time);

        await problem.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
        Assert.Equal("901", httpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_RetryAfterInThePast_SendsZeroSeconds()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        IResult problem = Application.Common.Result.Result
            .Failure(new LockedOutError(time.GetUtcNow().AddMinutes(-1)))
            .ToProblem();
        DefaultHttpContext httpContext = CreateHttpContext(time);

        await problem.ExecuteAsync(httpContext);

        Assert.Equal("0", httpContext.Response.Headers.RetryAfter.ToString());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ToProblem_ConsentRequiredError_Returns400WithAccountExistsAndEmailRequired(bool accountExists, bool emailRequired)
    {
        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(
            Application.Common.Result.Result.Failure(new ConsentRequiredError(accountExists, emailRequired)).ToProblem());

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(ConsentRequiredError.ErrorCode, problem.ProblemDetails.Extensions[ResultExtensions.CodeExtension]);
        Assert.Equal(accountExists, problem.ProblemDetails.Extensions[ResultExtensions.AccountExistsExtension]);
        Assert.Equal(emailRequired, problem.ProblemDetails.Extensions[ResultExtensions.EmailRequiredExtension]);
    }

    [Fact]
    public void ToProblem_OtherError_HasNoConsentExtensions()
    {
        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(
            Application.Common.Result.Result.Failure(Error.Validation("Auth.ConsentRequired", "Opis")).ToProblem());

        Assert.DoesNotContain(ResultExtensions.AccountExistsExtension, problem.ProblemDetails.Extensions.Keys);
        Assert.DoesNotContain(ResultExtensions.EmailRequiredExtension, problem.ProblemDetails.Extensions.Keys);
    }

    [Fact]
    public void ToProblem_SuccessfulResult_Throws()
    {
        Application.Common.Result.Result result = Application.Common.Result.Result.Success();

        Assert.Throws<InvalidOperationException>(() => result.ToProblem());
    }

    private static DefaultHttpContext CreateHttpContext(TimeProvider time)
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(time)
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };
    }
}