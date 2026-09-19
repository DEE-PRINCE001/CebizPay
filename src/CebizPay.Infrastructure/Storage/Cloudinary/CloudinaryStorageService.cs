#pragma warning disable CA1848, CA1873, CA1305, CS1591
using CebizPay.Application.Common.Interfaces.Storage;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Storage.Cloudinary;

/// <summary>
/// Production-grade implementation of Cloudinary document and image storage with MIME and magic number validation.
/// </summary>
public sealed class CloudinaryStorageService : ICloudinaryStorageService
{
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryStorageService> _logger;
    private readonly CloudinaryDotNet.Cloudinary? _cloudinary;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    /// <summary>
    /// Initializes a new instance of <see cref="CloudinaryStorageService"/>.
    /// </summary>
    public CloudinaryStorageService(
        IOptions<CloudinaryOptions> options,
        ILogger<CloudinaryStorageService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.Enabled &&
            !string.IsNullOrWhiteSpace(_options.CloudName) &&
            !string.IsNullOrWhiteSpace(_options.ApiKey) &&
            !string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            var account = new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret);
            _cloudinary = new CloudinaryDotNet.Cloudinary(account) { Api = { Secure = true } };
        }
    }

    /// <inheritdoc/>
    public async Task<CloudinaryUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var bytes = memoryStream.ToArray();

        return await UploadAsync(bytes, fileName, contentType, folder, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CloudinaryUploadResult> UploadAsync(
        byte[] fileBytes,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        if (fileBytes.Length == 0)
        {
            return CloudinaryUploadResult.Failure("Uploaded file is empty.");
        }

        var maxBytes = _options.MaxFileSizeMb * 1024 * 1024;
        if (fileBytes.Length > maxBytes)
        {
            return CloudinaryUploadResult.Failure($"File exceeds maximum permitted size of {_options.MaxFileSizeMb} MB.");
        }

        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        if (!AllowedMimeTypes.Contains(normalizedContentType))
        {
            return CloudinaryUploadResult.Failure($"Unsupported file type '{contentType}'. Allowed types: PDF, PNG, JPEG, WebP.");
        }

        if (!ValidateMagicNumber(fileBytes, normalizedContentType))
        {
            return CloudinaryUploadResult.Failure("File header signature does not match the stated content type.");
        }

        if (_cloudinary == null || !_options.Enabled)
        {
            _logger.LogWarning("Cloudinary is disabled or credentials not configured. Returning simulated mock reference.");
            var mockPublicId = $"{folder.Trim('/')}/simulated_{Guid.NewGuid():N}";
            var mockUrl = $"https://res.cloudinary.com/simulated/{mockPublicId}";
            return CloudinaryUploadResult.Success(mockUrl, mockPublicId, Path.GetExtension(fileName).TrimStart('.'), fileBytes.Length);
        }

        var sanitizedFolder = string.IsNullOrWhiteSpace(folder)
            ? _options.Folder
            : $"{_options.Folder.Trim('/')}/{folder.Trim('/')}";

        var safeFileName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "_");
        var publicId = $"{safeFileName}_{Guid.NewGuid():N}"[..Math.Min(40, safeFileName.Length + 17)];

        try
        {
            using var uploadStream = new MemoryStream(fileBytes);

            if (normalizedContentType == "application/pdf")
            {
                var rawParams = new RawUploadParams
                {
                    File = new FileDescription(fileName, uploadStream),
                    Folder = sanitizedFolder,
                    PublicId = publicId,
                    UseFilename = false,
                    UniqueFilename = true
                };

                var uploadResult = await _cloudinary.UploadAsync(rawParams).ConfigureAwait(false);
                if (uploadResult.Error != null)
                {
                    _logger.LogError("Cloudinary raw upload error: {ErrorMessage}", uploadResult.Error.Message);
                    return CloudinaryUploadResult.Failure(uploadResult.Error.Message);
                }

                return CloudinaryUploadResult.Success(
                    uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty,
                    uploadResult.PublicId,
                    uploadResult.Format ?? "pdf",
                    uploadResult.Bytes);
            }
            else
            {
                var imageParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, uploadStream),
                    Folder = sanitizedFolder,
                    PublicId = publicId,
                    UseFilename = false,
                    UniqueFilename = true
                };

                var uploadResult = await _cloudinary.UploadAsync(imageParams).ConfigureAwait(false);
                if (uploadResult.Error != null)
                {
                    _logger.LogError("Cloudinary image upload error: {ErrorMessage}", uploadResult.Error.Message);
                    return CloudinaryUploadResult.Failure(uploadResult.Error.Message);
                }

                return CloudinaryUploadResult.Success(
                    uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty,
                    uploadResult.PublicId,
                    uploadResult.Format,
                    uploadResult.Bytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading asset to Cloudinary.");
            return CloudinaryUploadResult.Failure($"Storage upload failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            return false;

        if (_cloudinary == null || !_options.Enabled)
            return true;

        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams).ConfigureAwait(false);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Cloudinary resource {PublicId}.", publicId);
            return false;
        }
    }

    private static bool ValidateMagicNumber(byte[] bytes, string contentType)
    {
        if (bytes.Length < 4)
            return false;

        return contentType switch
        {
            "application/pdf" => bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46, // %PDF
            "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8 &&
                           bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                           bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            "image/webp" => bytes.Length >= 12 &&
                            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && // RIFF
                            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50, // WEBP
            _ => false
        };
    }
}
