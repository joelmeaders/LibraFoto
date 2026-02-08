# LibraFoto API Test Coverage Report

**Generated:** February 7, 2026  
**Version:** Current development branch

## 📊 Overall Coverage Summary

| Metric | Coverage | Details |
|--------|----------|---------|
| **Line Coverage** | **40.0%** | 2,908 / 7,265 lines covered |
| **Branch Coverage** | **31.5%** | 640 / 2,029 branches covered |
| **Method Coverage** | **52.8%** | 880 / 1,665 methods covered |
| **Test Results** | **95.8% passing** | 390 passed, 17 failed of 407 tests |
| **Assemblies** | 7 | |
| **Classes** | 156 | |
| **Files** | 102 | |

---

## 📁 Module-by-Module Breakdown

### ✅ **LibraFoto.Data** - 80.7% coverage
**Status:** Well-tested  
**Coverage:** 95.4% branches

**Strengths:**
- Excellent entity coverage (most at 100%)
- DbContext at 98.2% coverage
- Strong foundation for all modules

**Well-tested classes:**
- LibraFotoDbContext: 98.2%
- Album entity: 87.5%
- Photo entity: 85.7%
- DisplaySettings: 100%
- PhotoAlbum: 100%
- PhotoTag: 100%
- StorageProvider: 100%
- Tag: 100%
- User: 100%

**Gaps:**
- GuestLink entity: 0% (10 lines)
- PickerSession entity: 0% (8 lines)
- DbContextFactory: 0% (6 lines)
- DataModule: 46.4%

---

### ⚠️ **LibraFoto.Modules.Admin** - 50.1% coverage
**Status:** Mixed - Services strong, endpoints weak  
**Coverage:** 36.5% branches

**Strengths:**
- TagService: 95.7% ✅
- AlbumService: 90.2% ✅
- TagEndpoints: 58.7%
- AlbumEndpoints: 54.8%

**Critical Gaps:**
- **PhotoEndpoints: 0%** (97 lines, 26 branches) 🔴
- **PhotoService: 30.1%** (107/355 lines) - PRIORITY
- SystemService: 47.8% (135/282 lines)
- SystemEndpoints: 43.2% (16/37 lines)
- AdminModule: 0%

---

### ⚠️ **LibraFoto.Modules.Auth** - 63.7% coverage
**Status:** Good overall, critical services missing  
**Coverage:** 57.7% branches

**Strengths:**
- UserService: 100% ✅
- AuthService: 93.1% ✅
- UserEndpoints: 77.1%
- SetupEndpoints: 70.8%
- GuestLinkEndpoints: 66.9%
- AuthEndpoints: 64.7%

**Critical Gaps:**
- **GuestLinkService: 0%** (149 lines, 50 branches) 🔴
- **SetupService: 0%** (36 lines, 2 branches) 🔴
- AuthModule: 0%

---

### ⚠️ **LibraFoto.Modules.Display** - 61.4% coverage
**Status:** Services excellent, endpoints completely untested  
**Coverage:** 55.6% branches

**Strengths:**
- DisplaySettingsService: 92.3% ✅
- SlideshowService: 86.2% ✅
- DisplayConfigEndpoints: 69.9%

**Critical Gaps:**
- **DisplaySettingsEndpoints: 0%** (109 lines, 40 branches) 🔴
- **SlideshowEndpoints: 0%** (56 lines, 14 branches) 🔴
- DisplayModule: 0%

---

### 🔴 **LibraFoto.Modules.Media** - 18.4% coverage
**Status:** WORST MODULE - Massive gaps  
**Coverage:** 19.9% branches

**Partial Coverage:**
- MetadataService: 66.2%
- ThumbnailService: 64.4%
- MetadataResponse: 27.7%

**Critical Gaps:**
- **ImageProcessor: 0%** (197 lines, 92 branches) 🔴
- **ThumbnailEndpoints: 0%** (171 lines, 68 branches) 🔴
- **GeocodingService: 0%** (142 lines, 70 branches) 🔴
- **MetadataEndpoints: 0%** (77 lines, 22 branches) 🔴
- **PhotoEndpoints: 0%** (45 lines, 18 branches) 🔴
- MediaModule: 0%

---

### 🔴 **LibraFoto.Modules.Storage** - 28.3% coverage
**Status:** SECOND WORST - Largest untested files  
**Coverage:** 19.5% branches

**Strengths:**
- MediaScannerService: 89.2% ✅
- LocalStorageProvider: 81.3%
- StorageFileInfo: 92.8%

**Partial Coverage:**
- GooglePhotosProvider: 58.1%
- StorageEndpoints: 55.8%
- GooglePhotosPickerService: 41.4%

**Critical Gaps:**
- **GooglePhotosPickerEndpoints: 0%** (484 lines, 244 branches) - LARGEST FILE 🔴
- **UploadEndpoints: 0%** (443 lines, 124 branches) 🔴
- **SyncService: 0%** (264 lines, 48 branches) 🔴
- **StorageProviderFactory: 0%** (141 lines, 21 branches) 🔴
- **ImageImportService: 0%** (65 lines, 10 branches) 🔴
- GooglePhotosOAuthEndpoints: 19% (38/199 lines)
- StorageModule: 0%

---

### ⚠️ **LibraFoto.Shared** - 31.7% coverage
**Status:** Mixed  
**Coverage:** 0% branches

**Well-tested:**
- PagedResult<T>: 100%
- PaginationInfo: 100%
- ApiError: 60%

**Gaps:**
- LibraFotoDefaults: 0% (26 lines, 8 branches)

---

## 🎯 Priority Recommendations

### Critical Priority (Immediate Action Required)

**Largest Untested Components:**

1. 🔴 **Storage.GooglePhotosPickerEndpoints** - 484 lines, 244 branches
   - Handles Google Photos picker session management and media selection
   - Complex state management and external API integration

2. 🔴 **Storage.UploadEndpoints** - 443 lines, 124 branches
   - Core upload functionality for photos
   - File handling and validation logic

3. 🔴 **Storage.SyncService** - 264 lines, 48 branches
   - Synchronization between storage providers and local database
   - Critical for data consistency

4. 🔴 **Media.ImageProcessor** - 197 lines, 92 branches
   - Image manipulation and processing
   - Resizing, format conversion, optimization

5. 🔴 **Media.ThumbnailEndpoints** - 171 lines, 68 branches
   - Thumbnail generation and serving
   - Critical for UI performance

**Critical Business Logic:**

6. 🔴 **Auth.GuestLinkService** - 149 lines, 50 branches
   - Handles guest access links (authentication/security)
   - Security-critical component

7. 🔴 **Media.GeocodingService** - 142 lines, 70 branches
   - External API integration (Nominatim)
   - Reverse geocoding for photo locations

8. 🔴 **Storage.StorageProviderFactory** - 141 lines, 21 branches
   - Factory pattern for storage provider instantiation
   - Core architectural component

9. 🔴 **Admin.PhotoEndpoints** - 97 lines, 26 branches
   - Core photo management endpoints
   - CRUD operations for photos

### High Priority

**Partial Coverage Needing Improvement:**

- **Admin.PhotoService: 30.1%** → target 80%
  - 107 of 355 lines covered
  - Core photo business logic

- **Display.DisplaySettingsEndpoints: 0%** → target 80%
  - 109 lines, 40 branches
  - Display configuration management

- **Display.SlideshowEndpoints: 0%** → target 80%
  - 56 lines, 14 branches
  - Slideshow functionality

- **Auth.SetupService: 0%** → target 80%
  - 36 lines, 2 branches
  - Initial setup and configuration

- **Storage.GooglePhotosOAuthEndpoints: 19%** → target 80%
  - 38 of 199 lines covered
  - OAuth authentication flow

### Medium Priority

**Module Registration Classes:**
- All module classes (AdminModule, AuthModule, DisplayModule, MediaModule, StorageModule): 0%
- These register dependencies and endpoints
- Low complexity but should be tested for completeness

---

## 📈 Pattern Analysis

### Key Finding: Endpoint vs Service Coverage Gap

| Category | Average Coverage | Notes |
|----------|------------------|-------|
| **Service Classes** | ~73% | Most services well-tested with unit tests |
| **Endpoint Classes** | ~22% | Significant gap in endpoint/integration testing |
| **Entity Classes** | ~75% | Good coverage for data models |
| **Module Classes** | 0% | Registration code untested |

**Implication:** The codebase has good unit test coverage for services (business logic), but lacks integration tests for endpoints (HTTP layer). This means:
- Business logic is validated in isolation
- HTTP request/response handling is not tested
- Parameter binding and validation untested
- Error handling at the API boundary is untested

---

## 🧪 Recommended Testing Strategy

### 1. Endpoint Integration Tests
**Priority:** High  
**Approach:** Add endpoint tests using TUnit with test doubles

```csharp
// Example pattern for endpoint tests
[Test]
public async Task GetPhotos_ReturnsPagedResult()
{
    var photoService = Substitute.For<IPhotoService>();
    var result = await PhotoEndpoints.GetPhotos(photoService, ...);
    await Assert.That(result).IsTypeOf<Ok<PagedResult<PhotoDto>>>();
}
```

### 2. Media Module Focus
**Priority:** Critical  
**Components:**
- ImageProcessor: Image manipulation operations
- GeocodingService: External API integration
- ThumbnailEndpoints: Performance-critical path
- MetadataEndpoints: EXIF data extraction

**Testing approach:**
- Mock external dependencies (HTTP clients, file system)
- Test edge cases (corrupt images, large files, missing EXIF data)
- Performance tests for thumbnail generation

### 3. Storage Module Focus
**Priority:** Critical  
**Components:**
- GooglePhotosPickerEndpoints: Complex state machine
- UploadEndpoints: File upload handling
- SyncService: Data consistency logic
- StorageProviderFactory: Provider instantiation

**Testing approach:**
- Test upload validation (file size, type, security)
- Test sync conflict resolution
- Test provider factory with different configurations

### 4. Security Testing
**Priority:** Critical  
**Components:**
- GuestLinkService: Access control
- SetupService: Initial configuration security

**Testing approach:**
- Test authorization boundaries
- Test link expiration and access limits
- Test setup flow prevents re-initialization

### 5. Failing Tests Resolution
**Current failures:** 17 tests failing (mostly OperationCanceled exceptions)

**Investigation needed:**
- Thumbnail generation tests timing out
- Media scanner tests cancellation issues
- Slideshow sequence tests failing assertions
- Album cover photo foreign key constraint issue\

---

## 🔧 Commands

### Run tests with coverage
```bash
dotnet test --solution apps/api/LibraFoto.slnx -- --coverage --coverage-output-format cobertura --coverage-output TestResults/coverage.cobertura.xml
```

### Generate coverage report
```bash
reportgenerator -reports:"tests\*\bin\Debug\net10.0\TestResults\TestResults\*.cobertura.xml" -targetdir:"TestResults\CoverageReport" -reporttypes:"Html;TextSummary"
```

### View HTML report
```
TestResults/CoverageReport/index.html
```

---

## 📚 Resources

- [TUnit Documentation](https://github.com/thomhurst/TUnit)
- [Coverage Reports](../TestResults/CoverageReport/index.html)
- [Testing Best Practices](./development/testing-guide.md) *(to be created)*

---

## 📝 Notes

- **Test Framework:** TUnit (replaces xUnit/NUnit/MSTest)
- **Mocking Framework:** NSubstitute
- **Database Testing:** In-memory SQLite via EF Core
- **Coverage Tool:** dotnet-coverage (built into TUnit)
- **Report Generator:** ReportGenerator Global Tool

---

*This report provides a snapshot of test coverage. Coverage should be tracked over time to ensure continuous improvement and prevent regression.*
