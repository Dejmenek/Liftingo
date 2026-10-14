namespace Liftingo.Application.Common.Messaging;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result.Result> HandleAsync(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result.Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct);
}