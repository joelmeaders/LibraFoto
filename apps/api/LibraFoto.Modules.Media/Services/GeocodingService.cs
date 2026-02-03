using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LibraFoto.Modules.Media.Models;

namespace LibraFoto.Modules.Media.Services;

/// <summary>
/// Service for reverse geocoding coordinates using OpenStreetMap Nominatim.
/// Rate limited to 1 request per second per Nominatim usage policy.
/// </summary>
public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _requestsThisMinute;
    private DateTime _minuteStart = DateTime.UtcNow;
    private static readonly TimeSpan RateLimit = TimeSpan.FromSeconds(1);
    private const int MaxRequestsPerMinute = 60;

    public GeocodingService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LibraFoto/1.0 (Digital Picture Frame)");
        }
    }

    public bool IsRateLimited
    {
        get
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            return timeSinceLastRequest < RateLimit || _requestsThisMinute >= MaxRequestsPerMinute;
        }
    }

    public (int Used, int Remaining, TimeSpan ResetIn) GetRateLimitStatus()
    {
        ResetMinuteCounterIfNeeded();
        var remaining = MaxRequestsPerMinute - _requestsThisMinute;
        var resetIn = TimeSpan.FromMinutes(1) - (DateTime.UtcNow - _minuteStart);
        if (resetIn < TimeSpan.Zero) resetIn = TimeSpan.Zero;
        return (_requestsThisMinute, remaining, resetIn);
    }

    public async Task<GeocodingResult> ReverseGeocodeAsync(
        double latitude, 
        double longitude, 
        CancellationToken cancellationToken = default)
    {
        await WaitForRateLimitAsync(cancellationToken);

        try
        {
            var url = $"reverse?format=jsonv2&lat={latitude}&lon={longitude}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            _lastRequestTime = DateTime.UtcNow;
            IncrementRequestCounter();

            if (!response.IsSuccessStatusCode)
            {
                return CreateEmptyResult(latitude, longitude);
            }

            var result = await response.Content.ReadFromJsonAsync<NominatimResponse>(cancellationToken);
            if (result == null)
            {
                return CreateEmptyResult(latitude, longitude);
            }

            return new GeocodingResult
            {
                DisplayName = FormatDisplayName(result),
                City = result.Address?.City ?? result.Address?.Town ?? result.Address?.Village,
                State = result.Address?.State,
                Country = result.Address?.Country,
                CountryCode = result.Address?.CountryCode?.ToUpperInvariant(),
                Latitude = latitude,
                Longitude = longitude
            };
        }
        catch (Exception)
        {
            return CreateEmptyResult(latitude, longitude);
        }
    }

    public async Task<string?> GetLocationNameAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var result = await ReverseGeocodeAsync(latitude, longitude, cancellationToken);
        return result.DisplayName;
    }

    public async IAsyncEnumerable<GeocodingResult> BatchReverseGeocodeAsync(
        IEnumerable<(double Latitude, double Longitude)> coordinates,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (latitude, longitude) in coordinates)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return await ReverseGeocodeAsync(latitude, longitude, cancellationToken);
        }
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            ResetMinuteCounterIfNeeded();

            // Wait if we've hit the per-minute limit
            while (_requestsThisMinute >= MaxRequestsPerMinute)
            {
                var waitTime = TimeSpan.FromMinutes(1) - (DateTime.UtcNow - _minuteStart);
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                ResetMinuteCounterIfNeeded();
            }

            // Wait for per-second rate limit
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < RateLimit)
            {
                await Task.Delay(RateLimit - timeSinceLastRequest, cancellationToken);
            }
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private void ResetMinuteCounterIfNeeded()
    {
        if (DateTime.UtcNow - _minuteStart >= TimeSpan.FromMinutes(1))
        {
            _minuteStart = DateTime.UtcNow;
            _requestsThisMinute = 0;
        }
    }

    private void IncrementRequestCounter()
    {
        ResetMinuteCounterIfNeeded();
        _requestsThisMinute++;
    }

    private static GeocodingResult CreateEmptyResult(double latitude, double longitude) => new()
    {
        Latitude = latitude,
        Longitude = longitude
    };

    private static string FormatDisplayName(NominatimResponse response)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(response.Address?.City))
            parts.Add(response.Address.City);
        else if (!string.IsNullOrWhiteSpace(response.Address?.Town))
            parts.Add(response.Address.Town);
        else if (!string.IsNullOrWhiteSpace(response.Address?.Village))
            parts.Add(response.Address.Village);

        if (!string.IsNullOrWhiteSpace(response.Address?.State))
            parts.Add(response.Address.State);

        if (!string.IsNullOrWhiteSpace(response.Address?.Country))
            parts.Add(response.Address.Country);

        return parts.Count > 0 ? string.Join(", ", parts) : response.DisplayName ?? string.Empty;
    }

    private class NominatimResponse
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}
