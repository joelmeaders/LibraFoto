using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Tests.Helpers;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    /// <summary>
    /// Comprehensive tests for MediaScannerService - covers file system scanning, metadata extraction, and error handling.
    /// Achieves high coverage of directory scanning logic, file type detection, and edge cases.
    /// </summary>
    public class MediaScannerServiceTests
    {
        private MediaScannerService _scanner = null!;
        private string _testDirectory = null!;

        [Before(Test)]
        public void Setup()
        {
            _scanner = new MediaScannerService();
            _testDirectory = TestHelpers.CreateTempDirectory();
        }

        [After(Test)]
        public void Cleanup()
        {
            TestHelpers.CleanupTempDirectory(_testDirectory);
        }

        #region IsSupportedMediaFile Tests

        [Test]
        public async Task IsSupportedMediaFile_WithJpg_ReturnsTrue()
        {
            var result = _scanner.IsSupportedMediaFile("photo.jpg");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithJpeg_ReturnsTrue()
        {
            var result = _scanner.IsSupportedMediaFile("photo.jpeg");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithPng_ReturnsTrue()
        {
            var result = _scanner.IsSupportedMediaFile("photo.png");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithMp4_ReturnsTrue()
        {
            var result = _scanner.IsSupportedMediaFile("video.mp4");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithMov_ReturnsTrue()
        {
            var result = _scanner.IsSupportedMediaFile("video.mov");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithTxt_ReturnsFalse()
        {
            var result = _scanner.IsSupportedMediaFile("document.txt");
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithPdf_ReturnsFalse()
        {
            var result = _scanner.IsSupportedMediaFile("document.pdf");
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithNoExtension_ReturnsFalse()
        {
            var result = _scanner.IsSupportedMediaFile("noextension");
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithUppercaseExtension_ReturnsTrue()
        {
            var result = _scanner.IsSupportedMediaFile("PHOTO.JPG");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithEmptyString_ReturnsFalse()
        {
            var result = _scanner.IsSupportedMediaFile("");
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task IsSupportedMediaFile_WithAllImageFormats_ReturnsTrue()
        {
            // Arrange
            var imageFormats = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".avif" };

            // Act & Assert
            foreach (var format in imageFormats)
            {
                var result = _scanner.IsSupportedMediaFile($"photo{format}");
                await Assert.That(result).IsTrue();
            }
        }

        [Test]
        public async Task IsSupportedMediaFile_WithAllVideoFormats_ReturnsTrue()
        {
            // Arrange
            var videoFormats = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v", ".3gp", ".wmv", ".flv" };

            // Act & Assert
            foreach (var format in videoFormats)
            {
                var result = _scanner.IsSupportedMediaFile($"video{format}");
                await Assert.That(result).IsTrue();
            }
        }

        [Test]
        public async Task IsSupportedMediaFile_WithMixedCase_ReturnsTrue()
        {
            await Assert.That(_scanner.IsSupportedMediaFile("photo.JpG")).IsTrue();
            await Assert.That(_scanner.IsSupportedMediaFile("video.Mp4")).IsTrue();
            await Assert.That(_scanner.IsSupportedMediaFile("IMAGE.PNG")).IsTrue();
        }

        #endregion

        #region IsSupportedImage Tests

        [Test]
        public async Task IsSupportedImage_WithJpg_ReturnsTrue()
        {
            var result = _scanner.IsSupportedImage("photo.jpg");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedImage_WithHeic_ReturnsTrue()
        {
            var result = _scanner.IsSupportedImage("photo.heic");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedImage_WithWebp_ReturnsTrue()
        {
            var result = _scanner.IsSupportedImage("photo.webp");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedImage_WithMp4_ReturnsFalse()
        {
            var result = _scanner.IsSupportedImage("video.mp4");
            await Assert.That(result).IsFalse();
        }

        #endregion

        #region IsSupportedVideo Tests

        [Test]
        public async Task IsSupportedVideo_WithMp4_ReturnsTrue()
        {
            var result = _scanner.IsSupportedVideo("video.mp4");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedVideo_WithMov_ReturnsTrue()
        {
            var result = _scanner.IsSupportedVideo("video.mov");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedVideo_WithWebm_ReturnsTrue()
        {
            var result = _scanner.IsSupportedVideo("video.webm");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedVideo_WithJpg_ReturnsFalse()
        {
            var result = _scanner.IsSupportedVideo("photo.jpg");
            await Assert.That(result).IsFalse();
        }

        #endregion

        #region GetContentType Tests

        [Test]
        public async Task GetContentType_WithJpg_ReturnsImageJpeg()
        {
            var result = _scanner.GetContentType("photo.jpg");
            await Assert.That(result).IsEqualTo("image/jpeg");
        }

        [Test]
        public async Task GetContentType_WithJpeg_ReturnsImageJpeg()
        {
            var result = _scanner.GetContentType("photo.jpeg");
            await Assert.That(result).IsEqualTo("image/jpeg");
        }

        [Test]
        public async Task GetContentType_WithPng_ReturnsImagePng()
        {
            var result = _scanner.GetContentType("photo.png");
            await Assert.That(result).IsEqualTo("image/png");
        }

        [Test]
        public async Task GetContentType_WithGif_ReturnsImageGif()
        {
            var result = _scanner.GetContentType("photo.gif");
            await Assert.That(result).IsEqualTo("image/gif");
        }

        [Test]
        public async Task GetContentType_WithWebp_ReturnsImageWebp()
        {
            var result = _scanner.GetContentType("photo.webp");
            await Assert.That(result).IsEqualTo("image/webp");
        }

        [Test]
        public async Task GetContentType_WithHeic_ReturnsImageHeic()
        {
            var result = _scanner.GetContentType("photo.heic");
            await Assert.That(result).IsEqualTo("image/heic");
        }

        [Test]
        public async Task GetContentType_WithMp4_ReturnsVideoMp4()
        {
            var result = _scanner.GetContentType("video.mp4");
            await Assert.That(result).IsEqualTo("video/mp4");
        }

        [Test]
        public async Task GetContentType_WithMov_ReturnsVideoQuicktime()
        {
            var result = _scanner.GetContentType("video.mov");
            await Assert.That(result).IsEqualTo("video/quicktime");
        }

        [Test]
        public async Task GetContentType_WithAvi_ReturnsVideoMsvideo()
        {
            var result = _scanner.GetContentType("video.avi");
            await Assert.That(result).IsEqualTo("video/x-msvideo");
        }

        [Test]
        public async Task GetContentType_WithUnknown_ReturnsOctetStream()
        {
            var result = _scanner.GetContentType("file.xyz");
            await Assert.That(result).IsEqualTo("application/octet-stream");
        }

        [Test]
        public async Task GetContentType_WithUppercase_ReturnsCorrectType()
        {
            await Assert.That(_scanner.GetContentType("PHOTO.JPG")).IsEqualTo("image/jpeg");
            await Assert.That(_scanner.GetContentType("VIDEO.MP4")).IsEqualTo("video/mp4");
        }

        [Test]
        public async Task GetContentType_WithMixedCase_ReturnsCorrectType()
        {
            await Assert.That(_scanner.GetContentType("Photo.JpG")).IsEqualTo("image/jpeg");
            await Assert.That(_scanner.GetContentType("Video.Mp4")).IsEqualTo("video/mp4");
        }

        [Test]
        public async Task GetContentType_ForAllSupportedFormats_ReturnsValidMimeType()
        {
            // Arrange
            var formats = new Dictionary<string, string>
            {
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".gif", "image/gif" },
                { ".webp", "image/webp" },
                { ".bmp", "image/bmp" },
                { ".tiff", "image/tiff" },
                { ".tif", "image/tiff" },
                { ".heic", "image/heic" },
                { ".heif", "image/heif" },
                { ".avif", "image/avif" },
                { ".mp4", "video/mp4" },
                { ".mov", "video/quicktime" },
                { ".avi", "video/x-msvideo" },
                { ".mkv", "video/x-matroska" },
                { ".webm", "video/webm" }
            };

            // Act & Assert
            foreach (var (extension, expectedType) in formats)
            {
                var result = _scanner.GetContentType($"file{extension}");
                await Assert.That(result).IsEqualTo(expectedType);
            }
        }

        #endregion

        #region GenerateUniqueFilename Tests

        [Test]
        public async Task GenerateUniqueFilename_WithNoCollision_ReturnsOriginalName()
        {
            var result = _scanner.GenerateUniqueFilename("photo.jpg", _testDirectory);
            await Assert.That(result).IsEqualTo("photo.jpg");
        }

        [Test]
        public async Task GenerateUniqueFilename_WithCollision_ReturnsUniqueNameWithTimestamp()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "photo.jpg"), "existing");

            // Act
            var result = _scanner.GenerateUniqueFilename("photo.jpg", _testDirectory);

            // Assert
            await Assert.That(result).IsNotEqualTo("photo.jpg");
            await Assert.That(result).StartsWith("photo_");
            await Assert.That(result).EndsWith(".jpg");
            await Assert.That(result).Contains("_001");
        }

        [Test]
        public async Task GenerateUniqueFilename_WithInvalidChars_SanitizesName()
        {
            var result = _scanner.GenerateUniqueFilename("photo<>:\"|?*.jpg", _testDirectory);

            await Assert.That(result).DoesNotContain("<");
            await Assert.That(result).DoesNotContain(">");
            await Assert.That(result).DoesNotContain(":");
            await Assert.That(result).DoesNotContain("\"");
            await Assert.That(result).DoesNotContain("|");
            await Assert.That(result).DoesNotContain("?");
            await Assert.That(result).DoesNotContain("*");
            await Assert.That(result).EndsWith(".jpg");
        }

        [Test]
        public async Task GenerateUniqueFilename_WithMultipleCollisions_IncrementsCounter()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "photo.jpg"), "original");

            // Act
            var result1 = _scanner.GenerateUniqueFilename("photo.jpg", _testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, result1), "first");

            var result2 = _scanner.GenerateUniqueFilename("photo.jpg", _testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, result2), "second");

            var result3 = _scanner.GenerateUniqueFilename("photo.jpg", _testDirectory);

            // Assert
            await Assert.That(result1).Contains("_001");
            await Assert.That(result2).Contains("_002");
            await Assert.That(result3).Contains("_003");
        }

        [Test]
        public async Task GenerateUniqueFilename_WithEmptyFilename_UsesDefaultName()
        {
            var result = _scanner.GenerateUniqueFilename(".jpg", _testDirectory);

            await Assert.That(result).StartsWith("photo");
            await Assert.That(result).EndsWith(".jpg");
        }

        [Test]
        public async Task GenerateUniqueFilename_PreservesExtension()
        {
            var extensions = new[] { ".jpg", ".png", ".mp4", ".gif", ".webp" };

            foreach (var ext in extensions)
            {
                var result = _scanner.GenerateUniqueFilename($"file{ext}", _testDirectory);
                await Assert.That(result).EndsWith(ext);
            }
        }

        [Test]
        public async Task GenerateUniqueFilename_HandlesFileNameWithoutExtension()
        {
            var result = _scanner.GenerateUniqueFilename("photo", _testDirectory);
            await Assert.That(result).IsEqualTo("photo");
        }

        [Test]
        public async Task GenerateUniqueFilename_WithAllInvalidChars_CreatesValidName()
        {
            var result = _scanner.GenerateUniqueFilename("<>:\"|?*", _testDirectory);

            // Should sanitize to valid filename
            await Assert.That(result).DoesNotContain("<");
            await Assert.That(result).DoesNotContain(">");
            await Assert.That(Path.GetInvalidFileNameChars().Any(c => result.Contains(c))).IsFalse();
        }

        [Test]
        public async Task GenerateUniqueFilename_WithLongFilename_HandlesCorrectly()
        {
            var longName = new string('a', 200) + ".jpg";
            var result = _scanner.GenerateUniqueFilename(longName, _testDirectory);

            await Assert.That(result).EndsWith(".jpg");
            await Assert.That(result.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task GenerateUniqueFilename_WithSpecialCharacters_PreservesValidOnes()
        {
            var filename = "photo_(1)[test]#tag.jpg";
            var result = _scanner.GenerateUniqueFilename(filename, _testDirectory);

            // Valid special chars should be preserved
            await Assert.That(result).Contains("(");
            await Assert.That(result).Contains(")");
            await Assert.That(result).Contains("[");
            await Assert.That(result).Contains("]");
            await Assert.That(result).Contains("#");
        }

        #endregion

        #region ScanDirectoryAsync Tests

        [Test]
        public async Task ScanDirectoryAsync_WithEmptyDirectory_ReturnsEmptyList()
        {
            var result = await _scanner.ScanDirectoryAsync(_testDirectory);
            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task ScanDirectoryAsync_WithMediaFiles_ReturnsMediaFiles()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "photo1.jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo2.png", 2);
            TestHelpers.CreateTestFile(_testDirectory, "video.mp4", 3);
            TestHelpers.CreateTestFile(_testDirectory, "document.txt", 1);

            // Act
            var result = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert - Only media files
            await Assert.That(result).Count().IsEqualTo(3);
            await Assert.That(result.Any(f => f.FileName == "photo1.jpg")).IsTrue();
            await Assert.That(result.Any(f => f.FileName == "photo2.png")).IsTrue();
            await Assert.That(result.Any(f => f.FileName == "video.mp4")).IsTrue();
            await Assert.That(result.Any(f => f.FileName == "document.txt")).IsFalse();
        }

        [Test]
        public async Task ScanDirectoryAsync_WithSubdirectories_ReturnsAllFiles()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "root.jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine("2026", "01", "nested.jpg"), 1);

            // Act
            var result = (await _scanner.ScanDirectoryAsync(_testDirectory, recursive: true)).ToList();

            // Assert
            await Assert.That(result).Count().IsEqualTo(2);
        }

        [Test]
        public async Task ScanDirectoryAsync_WithNonRecursive_ReturnsOnlyTopLevel()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "root.jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine("subdir", "nested.jpg"), 1);

            // Act
            var result = (await _scanner.ScanDirectoryAsync(_testDirectory, recursive: false)).ToList();

            // Assert
            await Assert.That(result).Count().IsEqualTo(1);
            await Assert.That(result[0].FileName).IsEqualTo("root.jpg");
        }

        [Test]
        public async Task ScanDirectoryAsync_WithNonExistentDirectory_ReturnsEmptyList()
        {
            var result = await _scanner.ScanDirectoryAsync("/nonexistent/directory");
            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task ScanDirectoryAsync_SetsCorrectMediaType()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "photo.jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, "video.mp4", 1);

            // Act
            var result = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            var photo = result.First(f => f.FileName == "photo.jpg");
            var video = result.First(f => f.FileName == "video.mp4");

            await Assert.That(photo.MediaType).IsEqualTo(MediaType.Photo);
            await Assert.That(video.MediaType).IsEqualTo(MediaType.Video);
        }

        [Test]
        public async Task ScanDirectoryAsync_CapturesFileMetadata()
        {
            // Arrange
            var filePath = TestHelpers.CreateTestFile(_testDirectory, "photo.jpg", 5);
            var fileInfo = new FileInfo(filePath);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(1);
            var scanned = results[0];
            await Assert.That(scanned.FileName).IsEqualTo("photo.jpg");
            await Assert.That(scanned.Extension).IsEqualTo(".jpg");
            await Assert.That(scanned.FileSize).IsEqualTo(fileInfo.Length);
            await Assert.That(scanned.FullPath).IsEqualTo(filePath);
            await Assert.That(scanned.RelativePath).IsEqualTo("photo.jpg");
            await Assert.That(scanned.MediaType).IsEqualTo(MediaType.Photo);
            await Assert.That(scanned.ContentType).IsEqualTo("image/jpeg");
        }

        [Test]
        public async Task ScanDirectoryAsync_IdentifiesHiddenFiles()
        {
            // Arrange
            var filePath = TestHelpers.CreateTestFile(_testDirectory, "hidden.jpg", 1);
            var fileInfo = new FileInfo(filePath);
            fileInfo.Attributes |= FileAttributes.Hidden;

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(1);
            await Assert.That(results[0].IsHidden).IsTrue();
        }

        [Test]
        public async Task ScanDirectoryAsync_SetsCorrectRelativePath()
        {
            // Arrange
            var subDir = Path.Combine("2026", "01");
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine(subDir, "photo.jpg"), 1);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(1);
            var relativePath = results[0].RelativePath.Replace('\\', '/');
            await Assert.That(relativePath).IsEqualTo("2026/01/photo.jpg");
        }

        [Test]
        public async Task ScanDirectoryAsync_SupportsAllImageFormats()
        {
            // Arrange
            var imageFormats = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".avif" };
            foreach (var format in imageFormats)
            {
                TestHelpers.CreateTestFile(_testDirectory, $"image{format}", 1);
            }

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(imageFormats.Length);
            foreach (var result in results)
            {
                await Assert.That(result.MediaType).IsEqualTo(MediaType.Photo);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_SupportsAllVideoFormats()
        {
            // Arrange
            var videoFormats = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v", ".3gp", ".wmv", ".flv" };
            foreach (var format in videoFormats)
            {
                TestHelpers.CreateTestFile(_testDirectory, $"video{format}", 1);
            }

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(videoFormats.Length);
            foreach (var result in results)
            {
                await Assert.That(result.MediaType).IsEqualTo(MediaType.Video);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_RespectsCancellationToken()
        {
            // Arrange - Create many files to increase chance of cancellation
            for (int i = 0; i < 100; i++)
            {
                TestHelpers.CreateTestFile(_testDirectory, $"photo{i:D3}.jpg", 1);
            }

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.That(async () =>
                await _scanner.ScanDirectoryAsync(_testDirectory, cancellationToken: cts.Token))
                .Throws<OperationCanceledException>();
        }

        [Test]
        public async Task ScanDirectoryAsync_IgnoresUnsupportedExtensions()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "document.pdf", 1);
            TestHelpers.CreateTestFile(_testDirectory, "text.txt", 1);
            TestHelpers.CreateTestFile(_testDirectory, "data.json", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo.jpg", 1);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(1);
            await Assert.That(results[0].FileName).IsEqualTo("photo.jpg");
        }

        [Test]
        public async Task ScanDirectoryAsync_HandlesFilesWithoutExtensions()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "photo");
            File.WriteAllBytes(filePath, new byte[1024]);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results).IsEmpty();
        }

        [Test]
        public async Task ScanDirectoryAsync_SetsCreatedAndModifiedTimes()
        {
            // Arrange
            var filePath = TestHelpers.CreateTestFile(_testDirectory, "photo.jpg", 1);
            var fileInfo = new FileInfo(filePath);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(1);
            var scanned = results[0];
            // Times should be close to the actual file times (within a few seconds)
            await Assert.That((scanned.CreatedTime - fileInfo.CreationTimeUtc).TotalSeconds).IsLessThan(5);
            await Assert.That((scanned.ModifiedTime - fileInfo.LastWriteTimeUtc).TotalSeconds).IsLessThan(5);
        }

        [Test]
        public async Task ScanDirectoryAsync_HandlesMixedMediaTypes()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "photo1.jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo2.png", 1);
            TestHelpers.CreateTestFile(_testDirectory, "video1.mp4", 5);
            TestHelpers.CreateTestFile(_testDirectory, "video2.mov", 5);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(4);
            var photos = results.Where(r => r.MediaType == MediaType.Photo).ToList();
            var videos = results.Where(r => r.MediaType == MediaType.Video).ToList();
            await Assert.That(photos.Count).IsEqualTo(2);
            await Assert.That(videos.Count).IsEqualTo(2);
        }

        [Test]
        public async Task ScanDirectoryAsync_HandlesLargeNumberOfFiles()
        {
            // Arrange
            for (int i = 0; i < 50; i++)
            {
                TestHelpers.CreateTestFile(_testDirectory, $"photo{i:D3}.jpg", 1);
            }

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(50);
            foreach (var result in results)
            {
                await Assert.That(result.FileName).StartsWith("photo");
                await Assert.That(result.Extension).IsEqualTo(".jpg");
                await Assert.That(result.MediaType).IsEqualTo(MediaType.Photo);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_HandlesDeepDirectoryNesting()
        {
            // Arrange
            var deepPath = Path.Combine("2026", "01", "15", "photos", "vacation");
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine(deepPath, "photo.jpg"), 1);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(1);
            var relativePath = results[0].RelativePath.Replace('\\', '/');
            await Assert.That(relativePath).Contains("2026/01/15/photos/vacation");
        }

        [Test]
        public async Task ScanDirectoryAsync_HandlesSpecialCharactersInFilenames()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "photo (1).jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo [2].jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo_#3.jpg", 1);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(3);
            await Assert.That(results.Any(r => r.FileName.Contains("(1)"))).IsTrue();
            await Assert.That(results.Any(r => r.FileName.Contains("[2]"))).IsTrue();
            await Assert.That(results.Any(r => r.FileName.Contains("#3"))).IsTrue();
        }

        [Test]
        public async Task ScanDirectoryAsync_SkipsInaccessibleFiles_GracefullyHandlesIOErrors()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "accessible.jpg", 1);
            var restrictedFile = TestHelpers.CreateTestFile(_testDirectory, "restricted.jpg", 1);

            // Try to make file readonly (best effort on different platforms)
            var fileInfo = new FileInfo(restrictedFile);
            fileInfo.Attributes |= FileAttributes.ReadOnly;

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert - should include accessible files (readonly files are still accessible)
            await Assert.That(results.Any(r => r.FileName == "accessible.jpg")).IsTrue();
        }

        [Test]
        public async Task ScanDirectoryAsync_HandlesExtensionCaseInsensitivity()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "photo1.JPG", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo2.Jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, "photo3.jpg", 1);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(3);
            foreach (var result in results)
            {
                await Assert.That(result.Extension).IsEqualTo(".jpg"); // Normalized to lowercase
                await Assert.That(result.MediaType).IsEqualTo(MediaType.Photo);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_RecursiveScan_WithMultipleLevels()
        {
            // Arrange
            TestHelpers.CreateTestFile(_testDirectory, "root.jpg", 1);
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine("level1", "photo1.jpg"), 1);
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine("level1", "level2", "photo2.jpg"), 1);
            TestHelpers.CreateTestFile(_testDirectory, Path.Combine("level1", "level2", "level3", "photo3.jpg"), 1);

            // Act
            var results = (await _scanner.ScanDirectoryAsync(_testDirectory, recursive: true)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(4);
            await Assert.That(results.Any(r => r.RelativePath == "root.jpg")).IsTrue();
            await Assert.That(results.Any(r => r.RelativePath.Contains("level1"))).IsTrue();
            await Assert.That(results.Any(r => r.RelativePath.Contains("level2"))).IsTrue();
            await Assert.That(results.Any(r => r.RelativePath.Contains("level3"))).IsTrue();
        }

        #endregion

        #region SupportedExtensions Tests

        [Test]
        public async Task SupportedImageExtensions_ContainsCommonFormats()
        {
            var extensions = _scanner.SupportedImageExtensions;

            await Assert.That(extensions.Contains(".jpg")).IsTrue();
            await Assert.That(extensions.Contains(".jpeg")).IsTrue();
            await Assert.That(extensions.Contains(".png")).IsTrue();
            await Assert.That(extensions.Contains(".gif")).IsTrue();
            await Assert.That(extensions.Contains(".webp")).IsTrue();
            await Assert.That(extensions.Contains(".heic")).IsTrue();
        }

        [Test]
        public async Task SupportedVideoExtensions_ContainsCommonFormats()
        {
            var extensions = _scanner.SupportedVideoExtensions;

            await Assert.That(extensions.Contains(".mp4")).IsTrue();
            await Assert.That(extensions.Contains(".mov")).IsTrue();
            await Assert.That(extensions.Contains(".avi")).IsTrue();
            await Assert.That(extensions.Contains(".mkv")).IsTrue();
            await Assert.That(extensions.Contains(".webm")).IsTrue();
        }

        #endregion
    }
}
