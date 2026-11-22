using FluentValidation;
using Trislab.Modules.Notifications.Contracts.v1.Commands;

namespace Trislab.Modules.Notifications.Features.v1.MarkAllNotificationsRead;

/// <summary>
/// No-op validator — the command carries no payload. Exists to satisfy the
/// arch test that requires every command handler to have a paired validator.
/// </summary>
public sealed class MarkAllNotificationsReadCommandValidator : AbstractValidator<MarkAllNotificationsReadCommand>
{
}
