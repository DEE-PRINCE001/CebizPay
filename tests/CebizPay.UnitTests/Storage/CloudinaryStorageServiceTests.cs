using System.ComponentModel.DataAnnotations;
using System.Text;
using CebizPay.Infrastructure.Storage.Cloudinary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Storage;

public sealed class CloudinaryStorageServiceTests
{
    private static CloudinaryStorageService CreateService(bool enabled = false, int maxFileSizeMb = 10)
    {
        var options = new CloudinaryOptions
        {
            Enabled = enabled,
            CloudName = "test_cloud",
            ApiKey = "test_key",
            ApiSecret = "test_secret",
            Folder = "test_folder",
            MaxFileSizeMb = maxFileSizeMb
        };

        return new CloudinaryStorageService(
            Options.Create(options),
            NullLogger<CloudinaryStorageService>.Instance);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ReturnsFailure()
    {
        var service = CreateService();
        var result = await service.UploadAsync(Array.Empty<byte>(), "test.pdf", "application/pdf", "kyb");

        Assert.False(result.Succeeded);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_ExceedsMaxSize_ReturnsFailure()
    {
        var service = CreateService(maxFileSizeMb: 1);
        var largeFile = new byte[2 * 1024 * 1024]; // 2MB

        var result = await service.UploadAsync(largeFile, "large.pdf", "application/pdf", "kyb");

        Assert.False(result.Succeeded);
        Assert.Contains("exceeds", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_UnsupportedMimeType_ReturnsFailure()
    {
        var service = CreateService();
        var bytes = Encoding.UTF8.GetBytes("fake file content");

        var result = await service.UploadAsync(bytes, "script.exe", "application/x-msdownload", "kyb");

        Assert.False(result.Succeeded);
        Assert.Contains("unsupported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_MagicNumberMismatch_ReturnsFailure()
    {
        var service = CreateService();
        // File claims to be PDF, but header is plain text
        var fakePdf = Encoding.UTF8.GetBytes("This is not a real PDF file header");

        var result = await service.UploadAsync(fakePdf, "fake.pdf", "application/pdf", "kyb");

        Assert.False(result.Succeeded);
        Assert.Contains("signature does not match", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_ValidPdfHeader_WhenDisabled_ReturnsSimulatedSuccess()
    {
        var service = CreateService(enabled: false);
        // Valid PDF magic bytes: %PDF
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        var result = await service.UploadAsync(pdfBytes, "valid_doc.pdf", "application/pdf", "kyb");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.SecureUrl);
        Assert.NotNull(result.PublicId);
        Assert.Contains("simulated", result.SecureUrl);
    }

    [Fact]
    public async Task UploadAsync_ValidPngHeader_WhenDisabled_ReturnsSimulatedSuccess()
    {
        var service = CreateService(enabled: false);
        // Valid PNG magic bytes
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

        var result = await service.UploadAsync(pngBytes, "valid_image.png", "image/png", "kyb");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.SecureUrl);
        Assert.NotNull(result.PublicId);
        Assert.Contains("simulated", result.SecureUrl);
    }

    [Fact]
    public async Task DeleteAsync_EmptyPublicId_ReturnsFalse()
    {
        var service = CreateService();
        var result = await service.DeleteAsync(string.Empty);

        Assert.False(result);
    }

    [Fact]
    public void CloudinaryOptions_Validation_ValidatesCorrectly()
    {
        var options = new CloudinaryOptions
        {
            Enabled = true,
            CloudName = "",
            ApiKey = "",
            ApiSecret = ""
        };

        var context = new ValidationContext(options);
        var results = options.Validate(context).ToList();

        Assert.Equal(3, results.Count);
    }
}
