using Liftingo.Application.Common.Result;

using ResultType = Liftingo.Application.Common.Result.Result;

namespace Liftingo.Api.UnitTests.Common.Result;

public class ResultTests
{
    private static readonly Error SomeError = Error.Failure("Test.Failure", "Something failed");

    [Fact]
    public void Success_NoValue_ReturnsSuccessWithNoError()
    {
        ResultType result = ResultType.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_WithError_ReturnsFailureCarryingError()
    {
        ResultType result = ResultType.Failure(SomeError);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void Constructor_SuccessWithError_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ResultType(true, SomeError));
    }

    [Fact]
    public void Constructor_FailureWithoutError_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ResultType(false, Error.None));
    }

    [Fact]
    public void SuccessOfT_WithValue_ReturnsSuccessWithValue()
    {
        Result<int> result = ResultType.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(Error.None, result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailureOfT_WithError_ReturnsFailureCarryingError()
    {
        Result<int> result = ResultType.Failure<int>(SomeError);

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void Value_FailureResult_ThrowsInvalidOperationException()
    {
        Result<string> result = ResultType.Failure<string>(SomeError);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_NonNullValue_ReturnsSuccessWithValue()
    {
        Result<string> result = "value";

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void ImplicitConversion_NullValue_ReturnsNullValueFailure()
    {
        string? value = null;

        Result<string> result = value;

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NullValue, result.Error);
    }

    [Fact]
    public void ValidationFailure_WithValidationError_ReturnsFailureCarryingValidationError()
    {
        var validationError = ValidationError.FromErrors([Error.Validation("Name.Empty", "Name is required")]);

        Result<int> result = Result<int>.ValidationFailure(validationError);

        Assert.True(result.IsFailure);
        Assert.Same(validationError, result.Error);
    }
}