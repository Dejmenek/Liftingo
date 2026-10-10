namespace Liftingo.Application.Common.Result;

/// <summary>One entry of the <c>errors</c> array in a validation ProblemDetails response.</summary>
public sealed record ErrorDetail(string Code, string Description);