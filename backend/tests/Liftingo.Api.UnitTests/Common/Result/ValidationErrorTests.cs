using Liftingo.Application.Common.Result;

namespace Liftingo.Api.UnitTests.Common.Result;

public class ValidationErrorTests
{
    [Fact]
    public void FromErrors_WithErrors_ExposesThemInOrder()
    {
        Error first = Error.Validation("Name.Empty", "Name is required");
        Error second = Error.Validation("Age.Negative", "Age must not be negative");

        ValidationError validationError = ValidationError.FromErrors([first, second]);

        Assert.Equal([first, second], validationError.Errors);
    }

    [Fact]
    public void FromErrors_Always_HasGeneralValidationCodeAndType()
    {
        ValidationError validationError = ValidationError.FromErrors([]);

        Assert.Equal("Validation.General", validationError.Code);
        Assert.Equal(ErrorType.Validation, validationError.Type);
    }

    [Fact]
    public void FromErrors_NoErrors_ExposesEmptyList()
    {
        ValidationError validationError = ValidationError.FromErrors([]);

        Assert.Empty(validationError.Errors);
    }
}