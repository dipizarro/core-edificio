namespace CoreEdificio.Application.Contracts.Bulk;

public record BulkItemResult<T>(int Index, bool Success, string? Error, T? Data);
