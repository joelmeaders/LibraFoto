using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Tests.Modules.Storage
{
    /// <summary>
    /// Tests for Storage module model records.
    /// </summary>
    public class StorageModelsTests
    {
        #region SyncResult Tests

        [Test]
        public async Task SyncResult_Successful_SetsCorrectProperties()
        {
            var start = DateTime.UtcNow.AddMinutes(-5);

            var result = SyncResult.Successful(
                providerId: 1,
                providerName: "Test Provider",
                added: 10,
                updated: 5,
                removed: 2,
                skipped: 3,
                total: 20,
                start: start);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.ProviderId).IsEqualTo(1);
            await Assert.That(result.ProviderName).IsEqualTo("Test Provider");
            await Assert.That(result.FilesAdded).IsEqualTo(10);
            await Assert.That(result.FilesUpdated).IsEqualTo(5);
            await Assert.That(result.FilesRemoved).IsEqualTo(2);
            await Assert.That(result.FilesSkipped).IsEqualTo(3);
            await Assert.That(result.TotalFilesFound).IsEqualTo(20);
            await Assert.That(result.TotalFilesProcessed).IsEqualTo(20); // 10+5+2+3
            await Assert.That(result.StartTime).IsEqualTo(start);
            await Assert.That(result.ErrorMessage).IsNull();
        }

        [Test]
        public async Task SyncResult_Failed_SetsCorrectProperties()
        {
            var start = DateTime.UtcNow;

            var result = SyncResult.Failed(
                providerId: 2,
                providerName: "Failed Provider",
                errorMessage: "Connection failed",
                start: start);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.ProviderId).IsEqualTo(2);
            await Assert.That(result.ProviderName).IsEqualTo("Failed Provider");
            await Assert.That(result.ErrorMessage).IsEqualTo("Connection failed");
            await Assert.That(result.FilesAdded).IsEqualTo(0);
        }

        [Test]
        public async Task SyncResult_Duration_CalculatesCorrectly()
        {
            var start = DateTime.UtcNow.AddMinutes(-10);
            var result = SyncResult.Successful(1, "Test", 0, 0, 0, 0, 0, start);

            await Assert.That(result.Duration.TotalMinutes).IsGreaterThanOrEqualTo(10);
        }

        #endregion

        #region UploadResult Tests

        [Test]
        public async Task UploadResult_Successful_SetsCorrectProperties()
        {
            var result = UploadResult.Successful(
                photoId: 123,
                fileId: "2026/01/photo.jpg",
                fileName: "photo.jpg",
                filePath: "2026/01/photo.jpg",
                fileSize: 5000,
                contentType: "image/jpeg");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.PhotoId).IsEqualTo(123);
            await Assert.That(result.FileId).IsEqualTo("2026/01/photo.jpg");
            await Assert.That(result.FileName).IsEqualTo("photo.jpg");
            await Assert.That(result.FilePath).IsEqualTo("2026/01/photo.jpg");
            await Assert.That(result.FileSize).IsEqualTo(5000);
            await Assert.That(result.ContentType).IsEqualTo("image/jpeg");
            await Assert.That(result.ErrorMessage).IsNull();
        }

        [Test]
        public async Task UploadResult_Failed_SetsCorrectProperties()
        {
            var result = UploadResult.Failed("File too large");

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.ErrorMessage).IsEqualTo("File too large");
            await Assert.That(result.PhotoId).IsNull();
            await Assert.That(result.FileId).IsNull();
        }

        #endregion

        #region BatchUploadResult Tests

        [Test]
        public async Task BatchUploadResult_AllSuccessful_ReturnsTrue()
        {
            var result = new BatchUploadResult
            {
                TotalFiles = 3,
                SuccessfulUploads = 3,
                FailedUploads = 0,
                Results = []
            };

            await Assert.That(result.AllSuccessful).IsTrue();
        }

        [Test]
        public async Task BatchUploadResult_WithFailures_ReturnsFalse()
        {
            var result = new BatchUploadResult
            {
                TotalFiles = 3,
                SuccessfulUploads = 2,
                FailedUploads = 1,
                Results = []
            };

            await Assert.That(result.AllSuccessful).IsFalse();
        }

        #endregion

        #region StorageProviderDto Tests

        [Test]
        public async Task StorageProviderDto_CanBeCreated()
        {
            var dto = new StorageProviderDto
            {
                Id = 1,
                Type = StorageProviderType.Local,
                Name = "Local Photos",
                IsEnabled = true,
                SupportsUpload = true,
                SupportsWatch = true,
                LastSyncDate = DateTime.UtcNow,
                PhotoCount = 100
            };

            await Assert.That(dto.Id).IsEqualTo(1);
            await Assert.That(dto.Name).IsEqualTo("Local Photos");
            await Assert.That(dto.Type).IsEqualTo(StorageProviderType.Local);
            await Assert.That(dto.IsEnabled).IsTrue();
            await Assert.That(dto.PhotoCount).IsEqualTo(100);
        }

        #endregion

        #region StorageFileInfo Tests

        [Test]
        public async Task StorageFileInfo_CanBeCreated()
        {
            var fileInfo = new StorageFileInfo
            {
                FileId = "2026/01/photo.jpg",
                FileName = "photo.jpg",
                FullPath = "/photos/2026/01/photo.jpg",
                FileSize = 1024,
                ContentType = "image/jpeg",
                MediaType = MediaType.Photo,
                CreatedDate = DateTime.UtcNow,
                IsFolder = false
            };

            await Assert.That(fileInfo.FileId).IsEqualTo("2026/01/photo.jpg");
            await Assert.That(fileInfo.FileName).IsEqualTo("photo.jpg");
            await Assert.That(fileInfo.MediaType).IsEqualTo(MediaType.Photo);
            await Assert.That(fileInfo.IsFolder).IsFalse();
        }

        #endregion

        #region SyncRequest Tests

        [Test]
        public async Task SyncRequest_DefaultValues_AreCorrect()
        {
            var request = new SyncRequest();

            await Assert.That(request.FullSync).IsFalse();
            await Assert.That(request.RemoveDeleted).IsTrue();
            await Assert.That(request.SkipExisting).IsTrue();
            await Assert.That(request.MaxFiles).IsEqualTo(0);
            await Assert.That(request.FolderId).IsNull();
            await Assert.That(request.Recursive).IsTrue();
        }

        [Test]
        public async Task SyncRequest_CanBeCustomized()
        {
            var request = new SyncRequest
            {
                FullSync = true,
                RemoveDeleted = false,
                SkipExisting = false,
                MaxFiles = 100,
                FolderId = "subfolder",
                Recursive = false
            };

            await Assert.That(request.FullSync).IsTrue();
            await Assert.That(request.RemoveDeleted).IsFalse();
            await Assert.That(request.MaxFiles).IsEqualTo(100);
            await Assert.That(request.FolderId).IsEqualTo("subfolder");
        }

        #endregion

        #region LocalStorageConfiguration Tests

        [Test]
        public async Task LocalStorageConfiguration_DefaultValues_AreCorrect()
        {
            var config = new LocalStorageConfiguration();

            await Assert.That(config.BasePath).IsEqualTo("./photos");
            await Assert.That(config.OrganizeByDate).IsTrue();
            await Assert.That(config.WatchForChanges).IsTrue();
        }

        [Test]
        public async Task LocalStorageConfiguration_CanBeCustomized()
        {
            var config = new LocalStorageConfiguration
            {
                BasePath = "/custom/path",
                OrganizeByDate = false,
                WatchForChanges = false
            };

            await Assert.That(config.BasePath).IsEqualTo("/custom/path");
            await Assert.That(config.OrganizeByDate).IsFalse();
            await Assert.That(config.WatchForChanges).IsFalse();
        }

        #endregion

        #region ScanResult Tests

        [Test]
        public async Task ScanResult_CanBeCreated()
        {
            var result = new ScanResult
            {
                ProviderId = 1,
                Success = true,
                TotalFilesFound = 100,
                NewFilesCount = 25,
                ExistingFilesCount = 75,
                NewFilesTotalSize = 50 * 1024 * 1024
            };

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.TotalFilesFound).IsEqualTo(100);
            await Assert.That(result.NewFilesCount).IsEqualTo(25);
            await Assert.That(result.ExistingFilesCount).IsEqualTo(75);
        }

        #endregion

        #region ScannedFile Tests

        [Test]
        public async Task ScannedFile_CanBeCreated()
        {
            var file = new ScannedFile
            {
                FullPath = "/photos/2026/01/test.jpg",
                RelativePath = "2026/01/test.jpg",
                FileName = "test.jpg",
                Extension = ".jpg",
                FileSize = 1024,
                ContentType = "image/jpeg",
                MediaType = MediaType.Photo,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                IsHidden = false
            };

            await Assert.That(file.FileName).IsEqualTo("test.jpg");
            await Assert.That(file.Extension).IsEqualTo(".jpg");
            await Assert.That(file.MediaType).IsEqualTo(MediaType.Photo);
        }

        #endregion
    }
}
