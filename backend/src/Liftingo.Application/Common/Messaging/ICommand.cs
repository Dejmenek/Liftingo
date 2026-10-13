namespace Liftingo.Application.Common.Messaging;

/// <summary>A use case that changes state and returns no data.</summary>
public interface ICommand;

/// <summary>A use case that changes state and returns <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse>;
