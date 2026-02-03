using System.Text.Json.Serialization;
using LibraFoto.Api.Endpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Auth.Endpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Display.Endpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Media.Endpoints;
using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;

namespace LibraFoto.Api;

/// <summary>
/// API information response for root endpoint.
/// </summary>
public record ApiInfo(string Name, string Version);

/// <summary>
/// JSON serializer context for API DTOs.
/// Add new types here as they are created for optimized JSON serialization.
/// </summary>
[JsonSerializable(typeof(ApiInfo))]
[JsonSerializable(typeof(ApiError))]
// Display module DTOs
[JsonSerializable(typeof(PhotoDto))]
[JsonSerializable(typeof(PhotoDto[]))]
[JsonSerializable(typeof(IReadOnlyList<PhotoDto>))]
[JsonSerializable(typeof(DisplaySettingsDto))]
[JsonSerializable(typeof(DisplaySettingsDto[]))]
[JsonSerializable(typeof(IReadOnlyList<DisplaySettingsDto>))]
[JsonSerializable(typeof(UpdateDisplaySettingsRequest))]
[JsonSerializable(typeof(PhotoCountResponse))]
[JsonSerializable(typeof(ResetResponse))]
[JsonSerializable(typeof(DisplayConfigResponse))]
[JsonSerializable(typeof(TransitionType))]
[JsonSerializable(typeof(SourceType))]
[JsonSerializable(typeof(MediaType))]
// Auth module DTOs
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(UpdateUserRequest))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserDto[]))]
[JsonSerializable(typeof(UserRole))]
[JsonSerializable(typeof(SetupRequest))]
[JsonSerializable(typeof(SetupStatusResponse))]
[JsonSerializable(typeof(CreateGuestLinkRequest))]
[JsonSerializable(typeof(GuestLinkDto))]
[JsonSerializable(typeof(GuestLinkDto[]))]
[JsonSerializable(typeof(GuestLinkValidationResponse))]
[JsonSerializable(typeof(GuestLinkPublicInfo))]
[JsonSerializable(typeof(RefreshTokenRequest))]
[JsonSerializable(typeof(TokenValidationResult))]
[JsonSerializable(typeof(PagedResult<UserDto>))]
[JsonSerializable(typeof(PagedResult<GuestLinkDto>))]
[JsonSerializable(typeof(PaginationInfo))]
// Storage module DTOs
[JsonSerializable(typeof(StorageProviderDto))]
[JsonSerializable(typeof(StorageProviderDto[]))]
[JsonSerializable(typeof(CreateStorageProviderRequest))]
[JsonSerializable(typeof(UpdateStorageProviderRequest))]
[JsonSerializable(typeof(LocalStorageConfiguration))]
[JsonSerializable(typeof(StorageFileInfo))]
[JsonSerializable(typeof(StorageFileInfo[]))]
[JsonSerializable(typeof(SyncRequest))]
[JsonSerializable(typeof(SyncResult))]
[JsonSerializable(typeof(SyncResult[]))]
[JsonSerializable(typeof(SyncStatus))]
[JsonSerializable(typeof(ScanResult))]
[JsonSerializable(typeof(UploadRequest))]
[JsonSerializable(typeof(UploadResult))]
[JsonSerializable(typeof(BatchUploadResult))]
[JsonSerializable(typeof(GuestUploadRequest))]
[JsonSerializable(typeof(ScannedFile))]
[JsonSerializable(typeof(ScannedFile[]))]
[JsonSerializable(typeof(CacheStatusResponse))]
[JsonSerializable(typeof(CachedFileDto))]
[JsonSerializable(typeof(CachedFileDto[]))]
[JsonSerializable(typeof(PagedResult<CachedFileDto>))]
// Media module DTOs
[JsonSerializable(typeof(MetadataResponse))]
[JsonSerializable(typeof(ThumbnailResult))]
[JsonSerializable(typeof(GeocodingResult))]
// Admin module DTOs
[JsonSerializable(typeof(PhotoListDto))]
[JsonSerializable(typeof(PhotoListDto[]))]
[JsonSerializable(typeof(PagedResult<PhotoListDto>))]
[JsonSerializable(typeof(PhotoCountDto))]
[JsonSerializable(typeof(PhotoDetailDto))]
[JsonSerializable(typeof(AlbumSummaryDto))]
[JsonSerializable(typeof(AlbumSummaryDto[]))]
[JsonSerializable(typeof(TagSummaryDto))]
[JsonSerializable(typeof(TagSummaryDto[]))]
[JsonSerializable(typeof(UpdatePhotoRequest))]
[JsonSerializable(typeof(PhotoFilterRequest))]
[JsonSerializable(typeof(BulkPhotoRequest))]
[JsonSerializable(typeof(AddPhotosToAlbumRequest))]
[JsonSerializable(typeof(RemovePhotosFromAlbumRequest))]
[JsonSerializable(typeof(AddTagsToPhotosRequest))]
[JsonSerializable(typeof(RemoveTagsFromPhotosRequest))]
[JsonSerializable(typeof(BulkOperationResult))]
[JsonSerializable(typeof(AlbumDto))]
[JsonSerializable(typeof(AlbumDto[]))]
[JsonSerializable(typeof(IReadOnlyList<AlbumDto>))]
[JsonSerializable(typeof(CreateAlbumRequest))]
[JsonSerializable(typeof(UpdateAlbumRequest))]
[JsonSerializable(typeof(ReorderPhotosRequest))]
[JsonSerializable(typeof(PhotoOrder))]
[JsonSerializable(typeof(PhotoOrder[]))]
[JsonSerializable(typeof(TagDto))]
[JsonSerializable(typeof(TagDto[]))]
[JsonSerializable(typeof(IReadOnlyList<TagDto>))]
[JsonSerializable(typeof(CreateTagRequest))]
[JsonSerializable(typeof(UpdateTagRequest))]
[JsonSerializable(typeof(AddPhotosToTagRequest))]
[JsonSerializable(typeof(RemovePhotosFromTagRequest))]
// Test endpoints DTOs
[JsonSerializable(typeof(ResetResult))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
