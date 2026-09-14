namespace ERCollectionCheckerJP.Application.Analysis;

public enum CollectionCheckErrorCode
{
    InvalidConfiguration,
    InvalidSlot,
    SaveNotFound,
    AccessDenied,
    SaveChangedWhileReading,
    SaveReadFailed,
    InvalidSave,
    DatabaseNotFound,
    DatabaseVersionMismatch,
    Unexpected,
}

public sealed class CollectionCheckException : Exception
{
    public CollectionCheckException(
        CollectionCheckErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public CollectionCheckErrorCode Code { get; }
}
