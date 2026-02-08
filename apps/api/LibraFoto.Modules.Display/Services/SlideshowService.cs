using System.Collections.Concurrent;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Display.Services
{
    /// <summary>
    /// Service for managing slideshow photo queue and rotation.
    /// Handles next photo selection based on display settings.
    /// Registered as singleton to maintain state, creates scoped DbContext per operation.
    /// </summary>
    public class SlideshowService : ISlideshowService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SlideshowService> _logger;

        // Thread-safe storage for slideshow state per settings ID
        private static readonly ConcurrentDictionary<long, SlideshowState> _states = new();

        public SlideshowService(
            IServiceScopeFactory scopeFactory,
            ILogger<SlideshowService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<PhotoDto?> GetNextPhotoAsync(long? settingsId = null, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IDisplaySettingsService>();

            var settings = await GetSettingsAsync(settingsService, settingsId, cancellationToken);
            if (settings == null)
            {
                return null;
            }

            var state = GetOrCreateState(settings.Id);
            var photos = await GetFilteredPhotoIdsAsync(scope, settings, cancellationToken);

            if (photos.Count == 0)
            {
                _logger.LogDebug("No photos available for settings {SettingsId}", settings.Id);
                return null;
            }

            // Rebuild queue if empty or settings changed
            if (state.PhotoQueue.Count == 0 || state.NeedsRefresh)
            {
                RebuildQueue(state, photos, settings.Shuffle);
            }

            // Get next photo ID from queue
            if (!state.PhotoQueue.TryDequeue(out var photoId))
            {
                // Queue exhausted, rebuild
                RebuildQueue(state, photos, settings.Shuffle);
                if (!state.PhotoQueue.TryDequeue(out photoId))
                {
                    return null;
                }
            }

            state.CurrentPhotoId = photoId;

            // Fetch full photo data
            return await GetPhotoDtoByIdAsync(scope, photoId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<PhotoDto?> GetCurrentPhotoAsync(long? settingsId = null, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IDisplaySettingsService>();

            var settings = await GetSettingsAsync(settingsService, settingsId, cancellationToken);
            if (settings == null)
            {
                return null;
            }

            var state = GetOrCreateState(settings.Id);

            if (!state.CurrentPhotoId.HasValue)
            {
                // No current photo, get the first one
                return await GetNextPhotoAsync(settingsId, cancellationToken);
            }

            return await GetPhotoDtoByIdAsync(scope, state.CurrentPhotoId.Value, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PhotoDto>> GetPreloadPhotosAsync(int count = 10, long? settingsId = null, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IDisplaySettingsService>();

            var settings = await GetSettingsAsync(settingsService, settingsId, cancellationToken);
            if (settings == null)
            {
                return [];
            }

            var state = GetOrCreateState(settings.Id);
            var photos = await GetFilteredPhotoIdsAsync(scope, settings, cancellationToken);

            if (photos.Count == 0)
            {
                return [];
            }

            // Rebuild queue if empty or needs refresh
            if (state.PhotoQueue.Count == 0 || state.NeedsRefresh)
            {
                RebuildQueue(state, photos, settings.Shuffle);
            }

            // Peek at the next N photos without dequeuing
            var preloadIds = state.PhotoQueue.Take(Math.Min(count, state.PhotoQueue.Count)).ToList();

            // If we need more, wrap around from the beginning
            if (preloadIds.Count < count && photos.Count > 0)
            {
                var remaining = count - preloadIds.Count;
                var additionalIds = settings.Shuffle
                    ? photos.OrderBy(_ => Random.Shared.Next()).Take(remaining)
                    : photos.Take(remaining);
                preloadIds.AddRange(additionalIds.Where(id => !preloadIds.Contains(id)));
            }

            // Fetch full photo data
            var result = new List<PhotoDto>();
            foreach (var id in preloadIds.Take(count))
            {
                var dto = await GetPhotoDtoByIdAsync(scope, id, cancellationToken);
                if (dto != null)
                {
                    result.Add(dto);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public void ResetSequence(long? settingsId = null)
        {
            var id = settingsId ?? 0;
            if (_states.TryGetValue(id, out var state))
            {
                state.PhotoQueue.Clear();
                state.CurrentPhotoId = null;
                state.NeedsRefresh = true;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetPhotoCountAsync(long? settingsId = null, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IDisplaySettingsService>();

            var settings = await GetSettingsAsync(settingsService, settingsId, cancellationToken);
            if (settings == null)
            {
                return 0;
            }

            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            var query = BuildPhotoQuery(dbContext, settings);
            return await query.CountAsync(cancellationToken);
        }

        private async Task<DisplaySettingsDto?> GetSettingsAsync(IDisplaySettingsService settingsService, long? settingsId, CancellationToken cancellationToken)
        {
            if (settingsId.HasValue)
            {
                return await settingsService.GetByIdAsync(settingsId.Value, cancellationToken);
            }

            return await settingsService.GetActiveSettingsAsync(cancellationToken);
        }

        private SlideshowState GetOrCreateState(long settingsId)
        {
            return _states.GetOrAdd(settingsId, _ => new SlideshowState());
        }

        private async Task<List<long>> GetFilteredPhotoIdsAsync(IServiceScope scope, DisplaySettingsDto settings, CancellationToken cancellationToken)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            var query = BuildPhotoQuery(dbContext, settings);
            return await query.Select(p => p.Id).ToListAsync(cancellationToken);
        }

        private IQueryable<Photo> BuildPhotoQuery(LibraFotoDbContext dbContext, DisplaySettingsDto settings)
        {
            IQueryable<Photo> query = dbContext.Photos.AsNoTracking();

            switch (settings.SourceType)
            {
                case SourceType.Album when settings.SourceId.HasValue:
                    query = query.Where(p => p.PhotoAlbums.Any(pa => pa.AlbumId == settings.SourceId.Value));
                    break;

                case SourceType.Tag when settings.SourceId.HasValue:
                    query = query.Where(p => p.PhotoTags.Any(pt => pt.TagId == settings.SourceId.Value));
                    break;

                case SourceType.All:
                default:
                    // No filter, show all photos
                    break;
            }

            return query;
        }

        private void RebuildQueue(SlideshowState state, List<long> photoIds, bool shuffle)
        {
            state.PhotoQueue.Clear();

            var orderedIds = shuffle
                ? photoIds.OrderBy(_ => Random.Shared.Next()).ToList()
                : photoIds;

            foreach (var id in orderedIds)
            {
                state.PhotoQueue.Enqueue(id);
            }

            state.NeedsRefresh = false;
            _logger.LogDebug("Rebuilt slideshow queue with {Count} photos, shuffle={Shuffle}", orderedIds.Count, shuffle);
        }

        private async Task<PhotoDto?> GetPhotoDtoByIdAsync(IServiceScope scope, long id, CancellationToken cancellationToken)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            var photo = await dbContext.Photos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (photo == null)
            {
                return null;
            }

            return MapToDto(photo);
        }

        private static PhotoDto MapToDto(Photo photo)
        {
            return new PhotoDto
            {
                Id = photo.Id,
                Url = $"/api/media/photos/{photo.Id}",
                ThumbnailUrl = photo.ThumbnailPath != null ? $"/api/media/photos/{photo.Id}/thumbnail" : null,
                DateTaken = photo.DateTaken,
                Location = photo.Location,
                MediaType = photo.MediaType,
                Duration = photo.Duration,
                Width = photo.Width,
                Height = photo.Height
            };
        }

        /// <summary>
        /// Internal state for tracking slideshow progress.
        /// </summary>
        private class SlideshowState
        {
            public Queue<long> PhotoQueue { get; } = new();
            public long? CurrentPhotoId { get; set; }
            public bool NeedsRefresh { get; set; } = true;
        }
    }
}
