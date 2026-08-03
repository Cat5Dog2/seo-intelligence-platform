namespace SeoIntelligence.Application.Accounts;

public sealed record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

public enum ChangePasswordError
{
    Unknown = 0,
    UserNotFound,
    InvalidCurrentPassword,
    NewPasswordMismatch,
    NewPasswordSameAsCurrent,
    InvalidNewPassword
}

public sealed record ChangePasswordResult(bool Succeeded, ChangePasswordError Error)
{
    public static ChangePasswordResult Success() => new(true, ChangePasswordError.Unknown);

    public static ChangePasswordResult Failure(ChangePasswordError error) => new(false, error);
}

public interface IAccountPasswordService
{
    Task<ChangePasswordResult> ChangePasswordAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default);
}
