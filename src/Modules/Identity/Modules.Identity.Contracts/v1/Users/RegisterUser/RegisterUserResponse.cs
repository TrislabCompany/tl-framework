namespace Trislab.Modules.Identity.Contracts.v1.Users.RegisterUser;

/// <summary>
/// The answer to a self-registration. It is the same whether or not the address already had an account, so it
/// says nothing about the address: the next step is always in the e-mail.
/// </summary>
/// <param name="Status">Always <c>check_your_email</c>.</param>
public record RegisterUserResponse(string Status)
{
    public static RegisterUserResponse CheckYourEmail { get; } = new("check_your_email");
}