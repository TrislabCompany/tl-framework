using Trislab.Modules.Files.Contracts.v1.DTOs;
using Mediator;

namespace Trislab.Modules.Files.Contracts.v1.Queries;

public sealed record GetFileMetadataQuery(Guid FileAssetId) : IQuery<FileAssetDto>;
