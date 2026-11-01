public sealed record DomainError(string Code, string Message);

public abstract record Result<T>
{
    private Result() { }

    public sealed record Ok(T Value) : Result<T>;
    public sealed record Error(DomainError ErrorDetails) : Result<T>;

    public static Result<T> Success(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return new Ok(value);
    }

    public static Result<T> Failure(string code, string message)
    {
        return new Error(new DomainError(code, message));
    }
}

public abstract record Option<T>
{
    private Option() { }

    public sealed record Some(T Value) : Option<T>;
    public sealed record None : Option<T>;
}

public static class ResultExtensions
{
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> transform)
    {
        switch (result)
        {
            case Result<T>.Ok ok:
                return Result<U>.Success(transform(ok.Value));
            case Result<T>.Error err:
                return Result<U>.Failure(err.ErrorDetails.Code, err.ErrorDetails.Message);
            default:
                throw new InvalidOperationException();
        }
    }

    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> next)
    {
        switch (result)
        {
            case Result<T>.Ok ok:
                return next(ok.Value);
            case Result<T>.Error err:
                return Result<U>.Failure(err.ErrorDetails.Code, err.ErrorDetails.Message);
            default:
                throw new InvalidOperationException();
        }
    }

    public static Result<T> ToResult<T>(this Option<T> option, string code, string message)
    {
        switch (option)
        {
            case Option<T>.Some some:
                return Result<T>.Success(some.Value);
            case Option<T>.None:
                return Result<T>.Failure(code, message);
            default:
                throw new InvalidOperationException();
        }
    }
}
