using Trislab.Modules.Auditing.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Auditing.Contracts.v1.GetAuditById;

public sealed record GetAuditByIdQuery(Guid Id) : IQuery<AuditDetailDto>;