using System.Text.Json;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using LibraFoto.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Storage
{
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

        [Test]
        public async Task BasePath_ReturnsConfiguredPath()
        {
            await Assert.That(_provider.BasePath).IsEqualTo(_tempDir);
        }

        #endregion

        #region Initialize Tests

        [Test]
        public async Task Initialize_WithNullConfiguration_UsesDefaults()
        {
            var tempDir = TestHelpers.CreateTempDirectory();
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                provider.Initialize(2, "Test Provider", null);

                await Assert.That(provider.ProviderId).IsEqualTo(2);
                await Assert.That(provider.DisplayName).IsEqualTo("Test Provider");
            }
            finally
            {
                TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task Initialize_WithEmptyConfiguration_UsesDefaults()
        {
            var tempDir = TestHelpers.CreateTempDirectory();
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                provider.Initialize(3, "Test Provider", "");

                await Assert.That(provider.ProviderId).IsEqualTo(3);
                await Assert.That(provider.DisplayName).IsEqualTo("Test Provider");
            }
            finally
            {
                TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task Initialize_WithInvalidJson_UsesDefaults()
        {
            var tempDir = TestHelpers.CreateTempDirectory();
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                provider.Initialize(4, "Test Provider", "invalid json {{{");

                await Assert.That(provider.ProviderId).IsEqualTo(4);
                await Assert.That(provider.DisplayName).IsEqualTo("Test Provider");
            }
            finally
            {
                TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task Initialize_CreatesBasePathIfNotExists()
        {
            var tempDir = Path.Combine(TestHelpers.CreateTempDirectory(), "new-subdir");
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                var config = new LocalStorageConfiguration { BasePath = tempDir };
                provider.Initialize(5, "Test Provider", JsonSerializer.Serialize(config));

                await Assert.That(Directory.Exists(tempDir)).IsTrue();
            }
            finally
            {
                var parentDir = Path.GetDirectoryName(tempDir);
                if (parentDir != null)
                {
                    TestHelpers.CleanupTempDirectory(parentDir);
                }
            }
        }

        [Test]
        public async Task Initialize_WithOrganizeByDateFalse_ConfiguresCorrectly()
        {
            var tempDir = TestHelpers.CreateTempDirectory();
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                var config = new LocalStorageConfiguration
                {
                    BasePath = tempDir,
                    OrganizeByDate = false
                };

                provider.Initialize(6, "No Date Organization", JsonSerializer.Serialize(config));

                await Assert.That(provider.ProviderId).IsEqualTo(6);
            }
            finally
            {
                TestHelpers.CleanupTempDirectory(tempDir);
            }
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

        [Test]
        public async Task GetFilesAsync_WithSpecificFolder_ReturnsOnlyThatFolder()
        {
            var subDir = Path.Combine("2026", "01");
            TestHelpers.CreateTestFile(_tempDir, "root.jpg");
            TestHelpers.CreateTestFile(_tempDir, Path.Combine(subDir, "nested1.jpg"));
            TestHelpers.CreateTestFile(_tempDir, Path.Combine(subDir, "nested2.png"));

            var files = (await _provider.GetFilesAsync(subDir)).ToList();

            await Assert.That(files).Count().IsEqualTo(2);
            await Assert.That(files.All(f => f.ParentFolderId?.StartsWith("2026") == true)).IsTrue();
        }

        [Test]
        public async Task GetFilesAsync_WithNonExistentFolder_ReturnsEmptyList()
        {
            var files = await _provider.GetFilesAsync("nonexistent/folder");

            await Assert.That(files).IsEmpty();
        }

        [Test]
        public async Task GetFilesAsync_FiltersThumbnailFiles()
        {
            TestHelpers.CreateTestFile(_tempDir, "photo.jpg");
            TestHelpers.CreateTestFile(_tempDir, Path.Combine(".thumbnails", "thumb1.jpg"));
            TestHelpers.CreateTestFile(_tempDir, Path.Combine("2026", ".thumbnails", "thumb2.jpg"));

            var files = (await _provider.GetFilesAsync(null)).ToList();

            await Assert.That(files).Count().IsEqualTo(1);
            await Assert.That(files[0].FileName).IsEqualTo("photo.jpg");
        }

        [Test]
        public async Task GetFilesAsync_NormalizesPathSeparators()
        {
            var subDir = Path.Combine("2026", "01");
            TestHelpers.CreateTestFile(_tempDir, Path.Combine(subDir, "test.jpg"));

            var files = (await _provider.GetFilesAsync(null)).ToList();

            await Assert.That(files).Count().IsEqualTo(1);
            await Assert.That(files[0].FileId).Contains("/"); // Should use forward slash
            await Assert.That(files[0].FileId).DoesNotContain("\\");
        }

        [Test]
        public async Task GetFilesAsync_FiltersNonMediaFiles()
        {
            TestHelpers.CreateTestFile(_tempDir, "photo.jpg");
            TestHelpers.CreateTestFile(_tempDir, "document.txt");
            TestHelpers.CreateTestFile(_tempDir, "readme.md");
            TestHelpers.CreateTestFile(_tempDir, "video.mp4");

            var files = (await _provider.GetFilesAsync(null)).ToList();

            await Assert.That(files).Count().IsEqualTo(2); // Only photo.jpg and video.mp4
        }

        [Test]
        public async Task GetFilesAsync_SetsCorrectParentFolderId()
        {
            var subDir = Path.Combine("2026", "01");
            TestHelpers.CreateTestFile(_tempDir, Path.Combine(subDir, "nested.jpg"));

            var files = (await _provider.GetFilesAsync(null)).ToList();

            await Assert.That(files).Count().IsEqualTo(1);
            await Assert.That(files[0].ParentFolderId).IsEqualTo("2026/01");
        }

        [Test]
        public async Task GetFilesAsync_WithCancellationToken_CanBeCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.That(async () => await _provider.GetFilesAsync(null, cts.Token))
                .Throws<OperationCanceledException>();
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

        [Test]
        public async Task UploadFileAsync_WithUnsupportedFileType_ReturnsFailure()
        {
            using var stream = new MemoryStream(new byte[100]);

            var result = await _provider.UploadFileAsync("document.txt", stream, "text/plain");

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.ErrorMessage).Contains("Unsupported file type");
        }

        [Test]
        public async Task UploadFileAsync_WithoutOrganizeByDate_StoresInRoot()
        {
            var tempDir = TestHelpers.CreateTempDirectory();
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                var config = new LocalStorageConfiguration
                {
                    BasePath = tempDir,
                    OrganizeByDate = false
                };
                provider.Initialize(10, "No Date Org", JsonSerializer.Serialize(config));

                using var stream = new MemoryStream(new byte[100]);
                var result = await provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

                await Assert.That(result.Success).IsTrue();
                await Assert.That(result.FilePath).IsEqualTo("test.jpg");
            }
            finally
            {
                TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task UploadFileAsync_PreservesFileContent()
        {
            var originalContent = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            using var stream = new MemoryStream(originalContent);

            var result = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

            await Assert.That(result.Success).IsTrue();

            var downloadedContent = await _provider.DownloadFileAsync(result.FileId!);
            await Assert.That(downloadedContent).IsEquivalentTo(originalContent);
        }

        [Test]
        public async Task UploadFileAsync_ReturnsCorrectContentType()
        {
            using var stream = new MemoryStream(new byte[100]);

            var result = await _provider.UploadFileAsync("test.png", stream, "image/png");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.ContentType).IsEqualTo("image/png");
        }

        [Test]
        public async Task UploadFileAsync_WithLargeFile_Succeeds()
        {
            // Create a 5MB file
            var largeContent = new byte[5 * 1024 * 1024];
            Random.Shared.NextBytes(largeContent);
            using var stream = new MemoryStream(largeContent);

            var result = await _provider.UploadFileAsync("large.jpg", stream, "image/jpeg");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.FileSize).IsEqualTo(largeContent.Length);
        }

        [Test]
        public async Task UploadFileAsync_WithSpecialCharacters_SanitizesFilename()
        {
            using var stream = new MemoryStream(new byte[100]);

            var result = await _provider.UploadFileAsync("test<>:|?.jpg", stream, "image/jpeg");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.FileName).DoesNotContain("<");
            await Assert.That(result.FileName).DoesNotContain(">");
            await Assert.That(result.FileName).DoesNotContain(":");
        }

        [Test]
        public async Task UploadFileAsync_WithVideoFile_Succeeds()
        {
            using var stream = new MemoryStream(new byte[1024]);

            var result = await _provider.UploadFileAsync("video.mp4", stream, "video/mp4");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.FileName).EndsWith(".mp4");
        }

        [Test]
        public async Task UploadFileAsync_WithCancellationToken_CanBeCancelled()
        {
            using var stream = new MemoryStream(new byte[10000]);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.That(async () => await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg", cts.Token))
                .Throws<OperationCanceledException>();
        }

        [Test]
        public async Task UploadFileAsync_WithEmptyFile_Succeeds()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());

            var result = await _provider.UploadFileAsync("empty.jpg", stream, "image/jpeg");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.FileSize).IsEqualTo(0);
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

        [Test]
        public async Task DownloadFileAsync_WithLargeFile_ReturnsCompleteContent()
        {
            // Create a 2MB file
            var largeContent = new byte[2 * 1024 * 1024];
            Random.Shared.NextBytes(largeContent);
            using var stream = new MemoryStream(largeContent);
            var uploadResult = await _provider.UploadFileAsync("large.jpg", stream, "image/jpeg");

            var downloadedContent = await _provider.DownloadFileAsync(uploadResult.FileId!);

            await Assert.That(downloadedContent.Length).IsEqualTo(largeContent.Length);
            await Assert.That(downloadedContent).IsEquivalentTo(largeContent);
        }

        [Test]
        public async Task DownloadFileAsync_WithEmptyFile_ReturnsEmptyArray()
        {
            using var stream = new MemoryStream(Array.Empty<byte>());
            var uploadResult = await _provider.UploadFileAsync("empty.jpg", stream, "image/jpeg");

            var downloadedContent = await _provider.DownloadFileAsync(uploadResult.FileId!);

            await Assert.That(downloadedContent).IsEmpty();
        }

        [Test]
        public async Task DownloadFileAsync_WithCancellationToken_CanBeCancelled()
        {
            using var stream = new MemoryStream(new byte[1024]);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.That(async () => await _provider.DownloadFileAsync(uploadResult.FileId!, cts.Token))
                .Throws<OperationCanceledException>();
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

        [Test]
        public async Task GetFileStreamAsync_WithLargeFile_SupportsStreaming()
        {
            // Create a 3MB file
            var largeContent = new byte[3 * 1024 * 1024];
            Random.Shared.NextBytes(largeContent);
            using var uploadStream = new MemoryStream(largeContent);
            var uploadResult = await _provider.UploadFileAsync("large.jpg", uploadStream, "image/jpeg");

            await using var fileStream = await _provider.GetFileStreamAsync(uploadResult.FileId!);
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);

            await Assert.That(memoryStream.ToArray().Length).IsEqualTo(largeContent.Length);
        }

        [Test]
        public async Task GetFileStreamAsync_SupportsMultipleReads()
        {
            var originalContent = new byte[] { 1, 2, 3, 4, 5 };
            using var uploadStream = new MemoryStream(originalContent);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", uploadStream, "image/jpeg");

            // Read the same file multiple times
            await using var stream1 = await _provider.GetFileStreamAsync(uploadResult.FileId!);
            await using var stream2 = await _provider.GetFileStreamAsync(uploadResult.FileId!);

            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();
            await stream1.CopyToAsync(ms1);
            await stream2.CopyToAsync(ms2);

            await Assert.That(ms1.ToArray()).IsEquivalentTo(originalContent);
            await Assert.That(ms2.ToArray()).IsEquivalentTo(originalContent);
        }

        [Test]
        public async Task GetFileStreamAsync_StreamCanBeReadPartially()
        {
            var originalContent = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            using var uploadStream = new MemoryStream(originalContent);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", uploadStream, "image/jpeg");

            await using var fileStream = await _provider.GetFileStreamAsync(uploadResult.FileId!);
            var buffer = new byte[3];
            var bytesRead = await fileStream.ReadAsync(buffer, 0, 3);

            await Assert.That(bytesRead).IsEqualTo(3);
            await Assert.That(buffer).IsEquivalentTo(new byte[] { 1, 2, 3 });
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

        [Test]
        public async Task DeleteFileAsync_ActuallyRemovesFile()
        {
            using var stream = new MemoryStream(new byte[100]);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

            var deleted = await _provider.DeleteFileAsync(uploadResult.FileId!);

            await Assert.That(deleted).IsTrue();
            await Assert.That(async () => await _provider.DownloadFileAsync(uploadResult.FileId!))
                .Throws<FileNotFoundException>();
        }

        [Test]
        public async Task DeleteFileAsync_CanDeleteMultipleFiles()
        {
            using var stream1 = new MemoryStream(new byte[100]);
            using var stream2 = new MemoryStream(new byte[100]);
            var result1 = await _provider.UploadFileAsync("test1.jpg", stream1, "image/jpeg");
            var result2 = await _provider.UploadFileAsync("test2.jpg", stream2, "image/jpeg");

            var deleted1 = await _provider.DeleteFileAsync(result1.FileId!);
            var deleted2 = await _provider.DeleteFileAsync(result2.FileId!);

            await Assert.That(deleted1).IsTrue();
            await Assert.That(deleted2).IsTrue();
        }

        [Test]
        public async Task DeleteFileAsync_WithPathTraversal_ReturnsFalseOrThrows()
        {
            // Should either return false or throw UnauthorizedAccessException
            try
            {
                var deleted = await _provider.DeleteFileAsync("../../../etc/passwd");
                await Assert.That(deleted).IsFalse();
            }
            catch (UnauthorizedAccessException)
            {
                // This is also acceptable
            }
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

        [Test]
        public async Task FileExistsAsync_AfterDeletion_ReturnsFalse()
        {
            using var stream = new MemoryStream(new byte[100]);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

            await _provider.DeleteFileAsync(uploadResult.FileId!);
            var exists = await _provider.FileExistsAsync(uploadResult.FileId!);

            await Assert.That(exists).IsFalse();
        }

        [Test]
        public async Task FileExistsAsync_WithPathInSubdirectory_WorksCorrectly()
        {
            using var stream = new MemoryStream(new byte[100]);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

            var exists = await _provider.FileExistsAsync(uploadResult.FileId!);

            await Assert.That(exists).IsTrue();
        }

        [Test]
        public async Task FileExistsAsync_WithPathTraversal_ThrowsUnauthorizedAccess()
        {
            await Assert.That(async () => await _provider.FileExistsAsync("../../../etc/passwd"))
                .Throws<UnauthorizedAccessException>();
        }

        #endregion

        #region TestConnectionAsync Tests

        [Test]
        public async Task TestConnectionAsync_WithValidPath_ReturnsTrue()
        {
            var result = await _provider.TestConnectionAsync();
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task TestConnectionAsync_CreatesMissingDirectory()
        {
            var tempDir = Path.Combine(TestHelpers.CreateTempDirectory(), "subdir");
            try
            {
                var configuration = Substitute.For<IConfiguration>();
                var logger = NullLogger<LocalStorageProvider>.Instance;
                var provider = new LocalStorageProvider(_scanner, configuration, logger);

                var config = new LocalStorageConfiguration { BasePath = tempDir };
                provider.Initialize(20, "Test Connection", JsonSerializer.Serialize(config));

                var result = await provider.TestConnectionAsync();

                await Assert.That(result).IsTrue();
                await Assert.That(Directory.Exists(tempDir)).IsTrue();
            }
            finally
            {
                var parentDir = Path.GetDirectoryName(tempDir);
                if (parentDir != null)
                {
                    TestHelpers.CleanupTempDirectory(parentDir);
                }
            }
        }

        [Test]
        public async Task TestConnectionAsync_VerifiesReadAccess()
        {
            var result = await _provider.TestConnectionAsync();
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task TestConnectionAsync_VerifiesWriteAccess()
        {
            var result = await _provider.TestConnectionAsync();
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task TestConnectionAsync_CleansUpTestFile()
        {
            await _provider.TestConnectionAsync();

            // Verify no test files left behind
            var files = Directory.GetFiles(_tempDir, ".librafoto-test-*");
            await Assert.That(files).IsEmpty();
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

        [Test]
        public async Task GetFileStreamAsync_WithAbsolutePath_ThrowsUnauthorizedAccess()
        {
            await Assert.That(async () => await _provider.GetFileStreamAsync("/etc/passwd"))
                .Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task DownloadFileAsync_WithAbsoluteWindowsPath_ThrowsUnauthorizedAccess()
        {
            await Assert.That(async () => await _provider.DownloadFileAsync("C:\\Windows\\System32\\config"))
                .Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task DeleteFileAsync_WithAbsolutePath_PreventsDeletion()
        {
            try
            {
                var result = await _provider.DeleteFileAsync("/etc/hosts");
                await Assert.That(result).IsFalse();
            }
            catch (UnauthorizedAccessException)
            {
                // This is also acceptable
            }
        }

        [Test]
        public async Task GetFileStreamAsync_WithRelativePath_Works()
        {
            using var stream = new MemoryStream(new byte[100]);
            var uploadResult = await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg");

            await using var fileStream = await _provider.GetFileStreamAsync(uploadResult.FileId!);

            await Assert.That(fileStream).IsNotNull();
        }

        [Test]
        public async Task GetFilesAsync_WithPathTraversalInFolderId_HandledSafely()
        {
            // Should either return empty or handle safely without accessing parent directories
            var files = await _provider.GetFilesAsync("../../etc");

            await Assert.That(files).IsEmpty();
        }

        #endregion
    }
}
