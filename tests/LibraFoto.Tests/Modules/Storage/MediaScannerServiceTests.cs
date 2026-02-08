using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Services;

namespace LibraFoto.Tests.Modules.Storage
{
    /// <summary>
    /// Tests for MediaScannerService.
    /// </summary>
    public class MediaScannerServiceTests
    {
        private readonly MediaScannerService _scanner = new();

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
        public async Task GetContentType_WithPng_ReturnsImagePng()
        {
            var result = _scanner.GetContentType("photo.png");
            await Assert.That(result).IsEqualTo("image/png");
        }

        [Test]
        public async Task GetContentType_WithMp4_ReturnsVideoMp4()
        {
            var result = _scanner.GetContentType("video.mp4");
            await Assert.That(result).IsEqualTo("video/mp4");
        }

        [Test]
        public async Task GetContentType_WithUnknown_ReturnsOctetStream()
        {
            var result = _scanner.GetContentType("file.xyz");
            await Assert.That(result).IsEqualTo("application/octet-stream");
        }

        #endregion

        #region GenerateUniqueFilename Tests

        [Test]
        public async Task GenerateUniqueFilename_WithNoCollision_ReturnsOriginalName()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                var result = _scanner.GenerateUniqueFilename("photo.jpg", tempDir);
                await Assert.That(result).IsEqualTo("photo.jpg");
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task GenerateUniqueFilename_WithCollision_ReturnsUniqueNameWithTimestamp()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                // Create existing file
                File.WriteAllText(Path.Combine(tempDir, "photo.jpg"), "existing");

                var result = _scanner.GenerateUniqueFilename("photo.jpg", tempDir);

                await Assert.That(result).IsNotEqualTo("photo.jpg");
                await Assert.That(result).StartsWith("photo_");
                await Assert.That(result).EndsWith(".jpg");
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task GenerateUniqueFilename_WithInvalidChars_SanitizesName()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                var result = _scanner.GenerateUniqueFilename("photo<>:\"|?*.jpg", tempDir);

                await Assert.That(result).DoesNotContain("<");
                await Assert.That(result).DoesNotContain(">");
                await Assert.That(result).DoesNotContain(":");
                await Assert.That(result).EndsWith(".jpg");
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        #endregion

        #region ScanDirectoryAsync Tests

        [Test]
        public async Task ScanDirectoryAsync_WithEmptyDirectory_ReturnsEmptyList()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                var result = await _scanner.ScanDirectoryAsync(tempDir);
                await Assert.That(result).IsEmpty();
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_WithMediaFiles_ReturnsMediaFiles()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                // Create test files
                Helpers.TestHelpers.CreateTestFile(tempDir, "photo1.jpg");
                Helpers.TestHelpers.CreateTestFile(tempDir, "photo2.png");
                Helpers.TestHelpers.CreateTestFile(tempDir, "video.mp4");
                Helpers.TestHelpers.CreateTestFile(tempDir, "document.txt");

                var result = (await _scanner.ScanDirectoryAsync(tempDir)).ToList();

                await Assert.That(result).Count().IsEqualTo(3); // Only media files
                await Assert.That(result.Any(f => f.FileName == "photo1.jpg")).IsTrue();
                await Assert.That(result.Any(f => f.FileName == "photo2.png")).IsTrue();
                await Assert.That(result.Any(f => f.FileName == "video.mp4")).IsTrue();
                await Assert.That(result.Any(f => f.FileName == "document.txt")).IsFalse();
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_WithSubdirectories_ReturnsAllFiles()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                // Create test files in subdirectories
                Helpers.TestHelpers.CreateTestFile(tempDir, "root.jpg");
                Helpers.TestHelpers.CreateTestFile(tempDir, Path.Combine("2026", "01", "nested.jpg"));

                var result = (await _scanner.ScanDirectoryAsync(tempDir, recursive: true)).ToList();

                await Assert.That(result).Count().IsEqualTo(2);
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task ScanDirectoryAsync_WithNonRecursive_ReturnsOnlyTopLevel()
        {
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                // Create test files in subdirectories
                Helpers.TestHelpers.CreateTestFile(tempDir, "root.jpg");
                Helpers.TestHelpers.CreateTestFile(tempDir, Path.Combine("subdir", "nested.jpg"));

                var result = (await _scanner.ScanDirectoryAsync(tempDir, recursive: false)).ToList();

                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0].FileName).IsEqualTo("root.jpg");
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
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
            var tempDir = Helpers.TestHelpers.CreateTempDirectory();
            try
            {
                Helpers.TestHelpers.CreateTestFile(tempDir, "photo.jpg");
                Helpers.TestHelpers.CreateTestFile(tempDir, "video.mp4");

                var result = (await _scanner.ScanDirectoryAsync(tempDir)).ToList();

                var photo = result.First(f => f.FileName == "photo.jpg");
                var video = result.First(f => f.FileName == "video.mp4");

                await Assert.That(photo.MediaType).IsEqualTo(MediaType.Photo);
                await Assert.That(video.MediaType).IsEqualTo(MediaType.Video);
            }
            finally
            {
                Helpers.TestHelpers.CleanupTempDirectory(tempDir);
            }
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
