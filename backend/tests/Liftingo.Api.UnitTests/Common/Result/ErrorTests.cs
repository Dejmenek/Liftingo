using Liftingo.Application.Common.Result;

namespace Liftingo.Api.UnitTests.Common.Result;

public class ErrorTests
{
    [Fact]
    public void None_Always_HasEmptyCodeAndDescription()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Description);
    }

    [Fact]
    public void NullValue_Always_IsGeneralNullFailure()
    {
        Assert.Equal("General.Null", Error.NullValue.Code);
        Assert.Equal(ErrorType.Failure, Error.NullValue.Type);
    }

    [Fact]
    public void General_Always_IsGeneralErrorFailure()
    {
        Assert.Equal("General.Error", Error.General.Code);
        Assert.Equal(ErrorType.Failure, Error.General.Type);
    }

    [Theory]
    [MemberData(nameof(FactoryCases))]
    public void Factory_CodeAndDescription_ReturnsErrorOfMatchingType(
        Func<string, string, Error> factory,
        ErrorType expectedType)
    {
        Error error = factory("Test.Code", "Test description");

        Assert.Equal("Test.Code", error.Code);
        Assert.Equal("Test description", error.Description);
        Assert.Equal(expectedType, error.Type);
    }

    public static TheoryData<Func<string, string, Error>, ErrorType> FactoryCases => new()
    {
        { Error.Failure, ErrorType.Failure },
        { Error.NotFound, ErrorType.NotFound },
        { Error.Validation, ErrorType.Validation },
        { Error.Conflict, ErrorType.Conflict },
        { Error.Unauthorized, ErrorType.Unauthorized },
    };

    [Fact]
    public void Equals_SameCodeDescriptionAndType_ReturnsTrue()
    {
        Assert.Equal(Error.NotFound("A.B", "x"), Error.NotFound("A.B", "x"));
    }

    [Fact]
    public void Equals_SameCodeDifferentType_ReturnsFalse()
    {
        Assert.NotEqual(Error.NotFound("A.B", "x"), Error.Conflict("A.B", "x"));
    }
}