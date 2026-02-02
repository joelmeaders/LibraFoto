# LibraFoto Google Photos Tests

Comprehensive test suite for the Google Photos storage provider integration.

## Test Coverage

This standalone test project validates the `GooglePhotosProvider` implementation with **25 passing tests**:

### Properties Tests (5 tests)

- `ProviderId_ReturnsInitializedId` - Validates provider ID is set correctly
- `DisplayName_ReturnsInitializedName` - Validates display name is set correctly
- `ProviderType_ReturnsGooglePhotos` - Confirms provider type enum value
- `SupportsUpload_ReturnsFalse` - Validates read-only nature
- `SupportsWatch_ReturnsFalse` - Confirms no file watching support

### Initialization Tests (3 tests)

- `Initialize_WithValidConfiguration_Succeeds` - Validates standard initialization
- `Initialize_WithEmptyConfiguration_UsesDefaults` - Tests null configuration handling
- `Initialize_WithInvalidJson_UsesDefaults` - Tests JSON deserialization error handling

### Read-Only Operations Tests (2 tests)

- `UploadFileAsync_ThrowsNotSupportedException` - Validates upload is disabled
- `DeleteFileAsync_ThrowsNotSupportedException` - Validates delete is disabled

### GetFilesAsync Tests (2 tests)

- `GetFilesAsync_WithoutAuthentication_ReturnsEmptyList` - Validates behavior without refresh token
- `GetFilesAsync_WithoutClientCredentials_ReturnsEmptyList` - Validates behavior without OAuth credentials

### TestConnectionAsync Tests (2 tests)

- `TestConnectionAsync_WithoutCredentials_ReturnsFalse` - Tests connection validation without credentials
- `TestConnectionAsync_WithIncompleteCredentials_ReturnsFalse` - Tests connection validation with partial credentials

### Configuration Tests (3 tests)

- `Configuration_AlbumName_DefaultsToLibraFoto` - Validates default album name
- `Configuration_EnableLocalCache_DefaultsToTrue` - Validates default cache setting
- `Configuration_MaxCacheSizeBytes_DefaultsTo5GB` - Validates default cache size (5GB)

### FileExistsAsync Tests (1 test)

- `FileExistsAsync_WithoutAuthentication_ReturnsFalse` - Validates file existence check without auth

### Download Tests (2 tests)

- `DownloadFileAsync_WithoutAuthentication_ThrowsInvalidOperationException` - Validates download fails without auth
- `GetFileStreamAsync_WithoutAuthentication_ThrowsInvalidOperationException` - Validates streaming fails without auth

### Multiple Provider Instances Tests (1 test)

- `MultipleProviders_CanBeInitializedIndependently` - Validates independent provider instances

### Provider Capability Tests (3 tests)

- `Provider_ImplementsIStorageProvider` - Validates interface implementation
- `Provider_IsReadOnly` - Confirms read-only capabilities
- `Provider_DoesNotSupportWatching` - Confirms no watch support

### Configuration Validation Tests (1 test)

- `Configuration_CanBeSerializedAndDeserialized` - Validates JSON serialization roundtrip

## Running Tests

### Quick Run

```powershell
cd tests\LibraFoto.GooglePhotos.Tests
dotnet run
```

### Build and Run

```powershell
cd tests\LibraFoto.GooglePhotos.Tests
dotnet build
dotnet run
```

### With Coverage

```powershell
cd tests\LibraFoto.GooglePhotos.Tests
dotnet run --coverage
```

## Test Framework

- **TUnit**: Modern test framework with attribute-based test discovery
- **No Imposter Dependency**: Uses custom `TestHttpClientFactory` to avoid test infrastructure issues in main test suite

## Test Philosophy

These tests focus on:

1. **Interface Compliance**: Validates `IStorageProvider` implementation
2. **Configuration Handling**: Tests defaults, validation, serialization
3. **Error Handling**: Validates graceful failure without credentials
4. **Read-Only Enforcement**: Confirms upload/delete operations are blocked
5. **Initialization**: Tests various configuration scenarios

## Expected Results

```
Test run summary: Passed! - bin\Debug\net10.0\LibraFoto.GooglePhotos.Tests.dll (net10.0|x64)
  total: 25
  failed: 0
  succeeded: 25
  skipped: 0
  duration: ~200-300ms
```

## Test Limitations

These tests do **NOT** perform actual Google API calls. They validate:

- Provider behavior without authentication
- Configuration defaults and validation
- Interface compliance
- Error handling

For integration tests with real Google Photos API, see `frontends/e2e/tests/` (Playwright E2E tests).

## Continuous Testing

These tests can be run independently of the main test suite and should pass consistently. They are self-contained and require no external dependencies or credentials.
