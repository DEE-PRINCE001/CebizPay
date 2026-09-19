namespace CebizPay.Application.Common.Interfaces.Storage;

/// <summary>
/// Result model for Cloudinary upload operations.
/// </summary>
public sealed record CloudinaryUploadResult(
    bool Succeeded,
    string? SecureUrl,
    string? PublicId,
    string? Format,
    long Bytes,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful upload result.
    /// </summary>
    public static CloudinaryUploadResult Success(string secureUrl, string publicId, string? format, long bytes) =>
        new(true, secureUrl, publicId, format, bytes, null);

    /// <summary>
    /// Creates a failed upload result.
    /// </summary>
    public static CloudinaryUploadResult Failure(string errorMessage) =>
        new(false, null, null, null, 0, errorMessage);
}

/// <summary>
/// Service abstraction for secure document and image storage using Cloudinary.
/// </summary>
public interface ICloudinaryStorageService
{
    /// <summary>
    /// Uploads a file stream to Cloudinary with MIME type, size, and magic number validation.
    /// </summary>
    Task<CloudinaryUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads raw file bytes to Cloudinary with MIME type, size, and magic number validation.
    /// </summary>
    Task<CloudinaryUploadResult> UploadAsync(
        byte[] fileBytes,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource from Cloudinary by its public ID.
    /// </summary>
    Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}
