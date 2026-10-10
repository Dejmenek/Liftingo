using Liftingo.Application.Common.Result;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Liftingo.Api.UnitTests.Common.Result;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
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
    public void ToProblem_SuccessfulResult_Throws()
    {
        Application.Common.Result.Result result = Application.Common.Result.Result.Success();

        Assert.Throws<InvalidOperationException>(() => result.ToProblem());
    }
}