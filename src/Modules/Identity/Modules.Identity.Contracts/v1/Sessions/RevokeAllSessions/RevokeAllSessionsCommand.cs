using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Sessions.RevokeAllSessions;

public sealed record RevokeAllSessionsCommand(Guid? ExceptSessionId = null) : ICommand<int>;