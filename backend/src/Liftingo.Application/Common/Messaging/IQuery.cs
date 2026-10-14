namespace Liftingo.Application.Common.Messaging;

/// <summary>A use case that only reads. Its handler never writes.</summary>
public interface IQuery<TResponse>;