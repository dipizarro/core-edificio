namespace CoreEdificio.Application.Common;

/// <summary>
/// Excepción lanzada cuando no se encuentra un recurso solicitado.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Excepción lanzada cuando ocurre un conflicto de estado (ej. duplicados).
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>
/// Excepción lanzada cuando fallan las validaciones de negocio o de entrada.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
