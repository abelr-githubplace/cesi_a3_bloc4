namespace Result
{
    public struct Empty
    {
        public static Result<Empty, E> EmptyOk<E>() => new Ok<Empty, E>(new Empty());
        public static Result<T, Empty> EmptyErr<T>() => new Err<T, Empty>(new Empty());
    }

    public record Ok<T, E>(T Value) : Result<T, E>;
    public record Err<T, E>(E Error) : Result<T, E>;

    public abstract record Result<T, E>
    {
        public bool IsOk => this is Ok<T, E>;
        public bool IsErr => this is Err<T, E>;

        public T? Ok()
        {
            return this switch
            {
                Ok<T, E> ok => ok.Value,
                Err<T, E> _ => default(T),
                _ => throw new InvalidOperationException(),
            };
        }

        public E? Err()
        {
            return this switch
            {
                Err<T, E> err => err.Error,
                Ok<T, E> _ => default(E),
                _ => throw new InvalidOperationException(),
            };
        }

        public T UnwrapOrDefault(T default_)
        {
            return this switch
            {
                Ok<T, E> ok => ok.Value,
                _ => default_,
            };
        }

        public T Unwrap()
        {
            return this switch
            {
                Ok<T, E> ok => ok.Value,
                Err<T, E> err => throw new Exception(err.Error?.ToString()),
                _ => throw new InvalidOperationException(),
            };
        }

        public E UnwrapErr()
        {
            return this switch
            {
                Err<T, E> err => err.Error,
                Ok<T, E> ok => throw new Exception(ok.Value?.ToString()),
                _ => throw new InvalidOperationException(),
            };
        }

        public Result<Mapped, E> Map<Mapped>(Func<T, Mapped> map)
        {
            return this switch
            {
                Ok<T, E> ok => new Ok<Mapped, E>(map(ok.Value)),
                Err<T, E> err => new Err<Mapped, E>(err.Error),
                _ => throw new InvalidOperationException(),
            };
        }

        public Result<T, Mapped> MapErr<Mapped>(Func<E, Mapped> map)
        {
            return this switch
            {
                Err<T, E> err => new Err<T, Mapped>(map(err.Error)),
                Ok<T, E> ok => new Ok<T, Mapped>(ok.Value),
                _ => throw new InvalidOperationException(),
            };
        }

        public TResult Match<TResult>(Func<T, TResult> onOk, Func<E, TResult> onErr)
        {
            return this switch
            {
                Ok<T, E> ok => onOk(ok.Value),
                Err<T, E> err => onErr(err.Error),
                _ => throw new InvalidOperationException(),
            };
        }
    }
}
