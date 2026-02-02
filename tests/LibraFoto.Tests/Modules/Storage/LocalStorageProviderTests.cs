using System.Text.Json;
using NSubstitute;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using LibraFoto.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraFoto.Tests.Modules.Storage;

/// <summary>
/// Tests for LocalStorageProvider.
/// </summary>
public class LocalStorageProviderTests
{
    private string _tempDir = null!;
    private LocalStorageProvider _provider = null!;
    private IMediaScannerService _scanner = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _tempDir = TestHelpers.CreateTempDirectory();
        _scanner = new LibraFoto.Modules.Storage.Services.MediaScannerService();

        var configuration = Substitute.For<IConfiguration>();
        var logger = NullLogger<LocalStorageProvider>.Instance;

        _provider = new LocalStorageProvider(_scanner, configuration, logger);

        var config = new LocalStorageConfiguration
        {
            BasePath = _tempDir,
            OrganizeByDate = true,
            WatchForChanges = false
        };

        _provider.Initialize(1, "Test Local Storage", JsonSerializer.Serialize(config));

        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        TestHelpers.CleanupTempDirectory(_tempDir);
        await Task.CompletedTask;
    }

    #region Properties Tests

    [Test]
    public async Task ProviderId_ReturnsInitializedId()
    {
        await Assert.That(_provider.ProviderId).IsEqualTo(1);
    }

    [Test]
    public async Task DisplayName_ReturnsInitializedName()
    {
        await Assert.That(_provider.DisplayName).IsEqualTo("Test Local Storage");
    }

    [Test]
    public async Task ProviderType_ReturnsLocal()
    {
        await Assert.That(_provider.ProviderType).IsEqualTo(StorageProviderType.Local);
    }

    [Test]
    public async Task SupportsUpload_ReturnsTrue()
    {
        await Assert.That(_provider.SupportsUpload).IsTrue();
    }

    [Test]
    public async Task SupportsWatch_ReturnsTrue()
    {
        await Assert.That(_provider.SupportsWatch).IsTrue();
    }

    #endregion

    #region GetFilesAsync Tests

    [Test]
    public async Task GetFilesAsync_WithEmptyDirectory_ReturnsEmptyList()
    {
        var files = await _provider.GetFilesAsync(null);
        await Assert.That(files).IsEmpty();
    }

    [Test]
    public async Task GetFilesAsync_WithMediaFiles_ReturnsFiles()
    {
        TestHelpers.CreateTestFile(_tempDir, "photo1.jpg");
        TestHelpers.CreateTestFile(_tempDir, "photo2.png");

        var files = (await _provider.GetFilesAsync(null)).ToList();

        await Assert.That(files).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetFilesAsync_ReturnsCorrectFileInfo()
    {
        TestHelpers.CreateTestFile(_tempDir, "test.jpg", sizeKb: 5);

        var files = (await _provider.GetFilesAsync(null)).ToList();

        await Assert.That(files).Count().IsEqualTo(1);
        var file = files[0];
        await Assert.That(file.FileName).IsEqualTo("test.jpg");
        await Assert.That(file.FileSize).IsEqualTo(5 * 1024);
        await Assert.That(file.MediaType).IsEqualTo(MediaType.Photo);
        await Assert.That(file.ContentType).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task GetFilesAsync_WithSubdirectory_ReturnsFilesRecursively()
    {
        TestHelpers.CreateTestFile(_tempDir, "root.jpg");
        TestHelpers.CreateTestFile(_tempDir, Path.Combine("2026", "01", "nested.jpg"));

        var files = (await _provider.GetFilesAsync(null)).ToList();

        await Assert.That(files).Count().IsEqualTo(2);
    }

    #endregion

    #region UploadFileAsync Tests

    [Test]
    public async Task UploadFileAsync_WithValidFile_ReturnsSuccess()
    {
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.FileName).IsNotNull();
        await Assert.That(result.FilePath).IsNotNull();
        await Assert.That(result.FileSize).IsEqualTo(1024);
    }

    [Test]
    public async Task UploadFileAsync_OrganizesByDate_CreatesYearMonthFolder()
    {
        using var stream = new MemoryStream(new byte[100]);

        var result = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

        await Assert.That(result.Success).IsTrue();
        // FilePath should contain year/month pattern
        await Assert.That(result.FilePath).Contains("/");
    }

    [Test]
    public async Task UploadFileAsync_WithDuplicateName_GeneratesUniqueName()
    {
        // Upload first file
        using var stream1 = new MemoryStream(new byte[100]);
        var result1 = await _provider.UploadFileAsync("test.jpg", stream1, "image/jpeg");

        // Upload second file with same name
        using var stream2 = new MemoryStream(new byte[100]);
        var result2 = await _provider.UploadFileAsync("test.jpg", stream2, "image/jpeg");

        await Assert.That(result1.Success).IsTrue();
        await Assert.That(result2.Success).IsTrue();
        await Assert.That(result1.FilePath).IsNotEqualTo(result2.FilePath);
    }

    [Test]
    public async Task UploadFileAsync_CreatesActualFile()
    {
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

        await Assert.That(result.Success).IsTrue();

        var exists = await _provider.FileExistsAsync(result.FileId!);
        await Assert.That(exists).IsTrue();
    }

    #endregion

    #region DownloadFileAsync Tests

    [Test]
    public async Task DownloadFileAsync_WithExistingFile_ReturnsContent()
    {
        var originalContent = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(originalContent);
        var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

        var downloadedContent = await _provider.DownloadFileAsync(uploadResult.FileId!);

        await Assert.That(downloadedContent).IsEquivalentTo(originalContent);
    }

    [Test]
    public async Task DownloadFileAsync_WithNonExistentFile_ThrowsFileNotFound()
    {
        await Assert.That(async () => await _provider.DownloadFileAsync("nonexistent.jpg"))
            .Throws<FileNotFoundException>();
    }

    #endregion

    #region GetFileStreamAsync Tests

    [Test]
    public async Task GetFileStreamAsync_WithExistingFile_ReturnsReadableStream()
    {
        var originalContent = new byte[] { 10, 20, 30 };
        using var uploadStream = new MemoryStream(originalContent);
        var uploadResult = await _provider.UploadFileAsync("test.jpg", uploadStream, "image/jpeg");

        await using var fileStream = await _provider.GetFileStreamAsync(uploadResult.FileId!);
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);

        await Assert.That(memoryStream.ToArray()).IsEquivalentTo(originalContent);
    }

    [Test]
    public async Task GetFileStreamAsync_WithNonExistentFile_ThrowsFileNotFound()
    {
        await Assert.That(async () => await _provider.GetFileStreamAsync("nonexistent.jpg"))
            .Throws<FileNotFoundException>();
    }

    #endregion

    #region DeleteFileAsync Tests

    [Test]
    public async Task DeleteFileAsync_WithExistingFile_ReturnsTrue()
    {
        using var stream = new MemoryStream(new byte[100]);
        var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

        var deleted = await _provider.DeleteFileAsync(uploadResult.FileId!);

        await Assert.That(deleted).IsTrue();
        await Assert.That(await _provider.FileExistsAsync(uploadResult.FileId!)).IsFalse();
    }

    [Test]
    public async Task DeleteFileAsync_WithNonExistentFile_ReturnsFalse()
    {
        var deleted = await _provider.DeleteFileAsync("nonexistent.jpg");
        await Assert.That(deleted).IsFalse();
    }

    #endregion

    #region FileExistsAsync Tests

    [Test]
    public async Task FileExistsAsync_WithExistingFile_ReturnsTrue()
    {
        using var stream = new MemoryStream(new byte[100]);
        var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

        var exists = await _provider.FileExistsAsync(uploadResult.FileId!);

        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task FileExistsAsync_WithNonExistentFile_ReturnsFalse()
    {
        var exists = await _provider.FileExistsAsync("nonexistent.jpg");
        await Assert.That(exists).IsFalse();
    }

    #endregion

    #region TestConnectionAsync Tests

    [Test]
    public async Task TestConnectionAsync_WithValidPath_ReturnsTrue()
    {
        var result = await _provider.TestConnectionAsync();
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Security Tests

    [Test]
    public async Task DownloadFileAsync_WithPathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.That(async () => await _provider.DownloadFileAsync("../../../etc/passwd"))
            .Throws<UnauthorizedAccessException>();
    }

    [Test]
    public async Task GetFileStreamAsync_WithPathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.That(async () => await _provider.GetFileStreamAsync("..\\..\\..\\windows\\system32\\config"))
            .Throws<UnauthorizedAccessException>();
    }

    #endregion
}
