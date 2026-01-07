namespace CoreEdificio.Application.Contracts.Bulk;

public record BulkResponse<T>(Guid CommunityId, int Total, int Created, int Failed, List<BulkItemResult<T>> Results);
