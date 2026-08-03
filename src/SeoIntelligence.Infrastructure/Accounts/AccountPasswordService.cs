using Microsoft.AspNetCore.Identity;
using SeoIntelligence.Application.Accounts;
using SeoIntelligence.Infrastructure.Identity;

namespace SeoIntelligence.Infrastructure.Accounts;

public sealed class AccountPasswordService(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
    : IAccountPasswordService
{
    public async Task<ChangePasswordResult> ChangePasswordAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(command.NewPassword, command.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return ChangePasswordResult.Failure(ChangePasswordError.NewPasswordMismatch);
        }

        if (string.Equals(command.CurrentPassword, command.NewPassword, StringComparison.Ordinal))
        {
            return ChangePasswordResult.Failure(ChangePasswordError.NewPasswordSameAsCurrent);
        }

        var user = await userManager.FindByIdAsync(command.UserId);
        if (user is null)
        {
            return ChangePasswordResult.Failure(ChangePasswordError.UserNotFound);
        }

        if (!await userManager.CheckPasswordAsync(user, command.CurrentPassword))
        {
            return ChangePasswordResult.Failure(ChangePasswordError.InvalidCurrentPassword);
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            command.CurrentPassword,
            command.NewPassword);

        if (!result.Succeeded)
        {
            return ChangePasswordResult.Failure(ChangePasswordError.InvalidNewPassword);
        }

        user.UpdatedAt = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);

        return ChangePasswordResult.Success();
    }
}
