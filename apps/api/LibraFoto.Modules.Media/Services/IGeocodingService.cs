using LibraFoto.Modules.Media.Models;

namespace LibraFoto.Modules.Media.Services
{
    /// <summary>
    /// Service for reverse geocoding GPS coordinates to location names.
    /// Uses OpenStreetMap Nominatim API with rate limiting.
    /// </summary>
    public interface IGeocodingService
    {
        /// <summary>
        /// Converts GPS coordinates to a human-readable location name.
        /// </summary>
        /// <param name="latitude">GPS latitude in decimal degrees.</param>
        /// <param name="longitude">GPS longitude in decimal degrees.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Geocoding result with location details.</returns>
        Task<GeocodingResult> ReverseGeocodeAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts GPS coordinates to a short location name (e.g., "Paris, France").
        /// </summary>
        /// <param name="latitude">GPS latitude in decimal degrees.</param>
        /// <param name="longitude">GPS longitude in decimal degrees.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Short location name or null if geocoding fails.</returns>
        Task<string?> GetLocationNameAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Batch geocodes multiple coordinates with automatic rate limiting.
        /// </summary>
        /// <param name="coordinates">Collection of (latitude, longitude) pairs.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Results for each coordinate pair.</returns>
        IAsyncEnumerable<GeocodingResult> BatchReverseGeocodeAsync(
            IEnumerable<(double Latitude, double Longitude)> coordinates,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current rate limit status.
        /// </summary>
        /// <returns>Tuple of (requestsUsed, requestsRemaining, resetTime).</returns>
        (int Used, int Remaining, TimeSpan ResetIn) GetRateLimitStatus();

        /// <summary>
        /// Whether the service is currently rate limited.
        /// </summary>
        bool IsRateLimited { get; }
    }
}
