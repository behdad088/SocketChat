namespace Identity.API.Services.Account;

public enum AccountErrorCode
{
    None = 0,
    ValidationFailed,
    Conflict,
    NotFound,
    InvalidCode,
    ExpiredCode,
    AlreadyActivated,
    ConcurrencyConflict
}

public sealed class AccountOperationResult
{
    public bool Succeeded { get; private init; }
    public AccountErrorCode ErrorCode { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static AccountOperationResult Success() => new() { Succeeded = true };

    public static AccountOperationResult Failure(AccountErrorCode errorCode, params string[] errors) =>
        new() { Succeeded = false, ErrorCode = errorCode, Errors = errors };
}

public sealed class AccountOperationResult<T>
{
    public bool Succeeded { get; private init; }
    public AccountErrorCode ErrorCode { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];
    public T? Value { get; private init; }

    public static AccountOperationResult<T> Success(T value) => new() { Succeeded = true, Value = value };

    public static AccountOperationResult<T> Failure(AccountErrorCode errorCode, params string[] errors) =>
        new() { Succeeded = false, ErrorCode = errorCode, Errors = errors };
}
