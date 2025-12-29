namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Represents the result of an image restore operation.
/// Provides detailed success/failure information with user-friendly error messages.
/// </summary>
public sealed class ImageRestoreResult
{
    /// <summary>
    /// Whether the restore operation succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// User-friendly error message if restore failed, null on success.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Technical error details for logging/debugging, null on success.
    /// </summary>
    public string? TechnicalDetails { get; }

    /// <summary>
    /// Error category for programmatic handling.
    /// </summary>
    public ImageRestoreErrorKind ErrorKind { get; }

    /// <summary>
    /// Number of objects successfully restored (valid even on partial failure).
    /// </summary>
    public int ObjectsRestored { get; }

    /// <summary>
    /// Maximum object ID from the image header.
    /// </summary>
    public int MaxObjectId { get; }

    private ImageRestoreResult(bool success, string? errorMessage, string? technicalDetails,
        ImageRestoreErrorKind errorKind, int objectsRestored, int maxObjectId)
    {
        Success = success;
        ErrorMessage = errorMessage;
        TechnicalDetails = technicalDetails;
        ErrorKind = errorKind;
        ObjectsRestored = objectsRestored;
        MaxObjectId = maxObjectId;
    }

    /// <summary>
    /// Creates a successful restore result.
    /// </summary>
    public static ImageRestoreResult Succeeded(int objectsRestored, int maxObjectId)
    {
        return new ImageRestoreResult(
            success: true,
            errorMessage: null,
            technicalDetails: null,
            errorKind: ImageRestoreErrorKind.None,
            objectsRestored: objectsRestored,
            maxObjectId: maxObjectId);
    }

    /// <summary>
    /// Creates a failure result for file not found.
    /// </summary>
    public static ImageRestoreResult FileNotFound(string filePath)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Image file not found: {filePath}",
            technicalDetails: $"The specified image file does not exist at path: {filePath}",
            errorKind: ImageRestoreErrorKind.FileNotFound,
            objectsRestored: 0,
            maxObjectId: 0);
    }

    /// <summary>
    /// Creates a failure result for file access error.
    /// </summary>
    public static ImageRestoreResult FileAccessError(string filePath, Exception ex)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Cannot access image file: {filePath}",
            technicalDetails: $"File access error: {ex.Message}",
            errorKind: ImageRestoreErrorKind.FileAccessError,
            objectsRestored: 0,
            maxObjectId: 0);
    }

    /// <summary>
    /// Creates a failure result for invalid file format (wrong magic number).
    /// </summary>
    public static ImageRestoreResult InvalidFormat(string filePath, string actualMagic)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Invalid image file format: {filePath}",
            technicalDetails: $"Expected magic 'WOST', found '{actualMagic}'. This file is not a valid Smalltalk image.",
            errorKind: ImageRestoreErrorKind.InvalidFormat,
            objectsRestored: 0,
            maxObjectId: 0);
    }

    /// <summary>
    /// Creates a failure result for unsupported version.
    /// </summary>
    public static ImageRestoreResult UnsupportedVersion(string filePath, int foundVersion, int supportedVersion)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Unsupported image version {foundVersion} in {filePath}",
            technicalDetails: $"This image was saved with version {foundVersion}, but only version {supportedVersion} is supported. " +
                             $"The image may have been created with a newer interpreter version.",
            errorKind: ImageRestoreErrorKind.UnsupportedVersion,
            objectsRestored: 0,
            maxObjectId: 0);
    }

    /// <summary>
    /// Creates a failure result for corrupted image data.
    /// </summary>
    public static ImageRestoreResult CorruptedData(string filePath, int objectIndex, string details)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Corrupted image data in {filePath}",
            technicalDetails: $"Error reading object at index {objectIndex}: {details}",
            errorKind: ImageRestoreErrorKind.CorruptedData,
            objectsRestored: objectIndex,
            maxObjectId: 0);
    }

    /// <summary>
    /// Creates a failure result for truncated file (unexpected end of file).
    /// </summary>
    public static ImageRestoreResult TruncatedFile(string filePath, int objectsRead, int expectedCount)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Image file is truncated: {filePath}",
            technicalDetails: $"Expected {expectedCount} objects but file ended after {objectsRead} objects. " +
                             $"The image file may have been incompletely written or corrupted.",
            errorKind: ImageRestoreErrorKind.TruncatedFile,
            objectsRestored: objectsRead,
            maxObjectId: 0);
    }

    /// <summary>
    /// Creates a failure result for an unexpected exception.
    /// </summary>
    public static ImageRestoreResult UnexpectedError(string filePath, Exception ex, int objectsRestored)
    {
        return new ImageRestoreResult(
            success: false,
            errorMessage: $"Unexpected error restoring image: {filePath}",
            technicalDetails: $"Exception: {ex.GetType().Name}: {ex.Message}",
            errorKind: ImageRestoreErrorKind.UnexpectedError,
            objectsRestored: objectsRestored,
            maxObjectId: 0);
    }

    /// <summary>
    /// Returns a formatted description of the result.
    /// </summary>
    public override string ToString()
    {
        if (Success)
            return $"Image restored successfully: {ObjectsRestored} objects, maxObjectId={MaxObjectId}";
        else
            return $"Image restore failed: {ErrorMessage} ({ErrorKind})";
    }
}

/// <summary>
/// Categories of image restore errors for programmatic handling.
/// </summary>
public enum ImageRestoreErrorKind
{
    /// <summary>No error - operation succeeded.</summary>
    None = 0,

    /// <summary>The specified image file does not exist.</summary>
    FileNotFound,

    /// <summary>Cannot read the file (permissions, locked, etc.).</summary>
    FileAccessError,

    /// <summary>File does not have valid image format (wrong magic number).</summary>
    InvalidFormat,

    /// <summary>Image version is newer than supported.</summary>
    UnsupportedVersion,

    /// <summary>Image data is corrupted or invalid.</summary>
    CorruptedData,

    /// <summary>Image file is incomplete/truncated.</summary>
    TruncatedFile,

    /// <summary>An unexpected exception occurred.</summary>
    UnexpectedError
}
