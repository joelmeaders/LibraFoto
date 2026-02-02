namespace LibraFoto.Modules.Media.Models;

/// <summary>
/// Result of reverse geocoding from GPS coordinates.
/// </summary>
public record GeocodingResult
{
    /// <summary>
    /// Whether the geocoding was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if geocoding failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Original latitude that was queried.
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Original longitude that was queried.
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Full formatted display name from the API.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Short, human-readable location name (e.g., "Paris, France").
    /// </summary>
    public string? ShortName { get; init; }

    /// <summary>
    /// Street address or house number if available.
    /// </summary>
    public string? Street { get; init; }

    /// <summary>
    /// Neighborhood or suburb name.
    /// </summary>
    public string? Neighborhood { get; init; }

    /// <summary>
    /// City or town name.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// County or district name.
    /// </summary>
    public string? County { get; init; }

    /// <summary>
    /// State or province name.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Postal/ZIP code.
    /// </summary>
    public string? PostalCode { get; init; }

    /// <summary>
    /// Country name.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g., "US", "FR").
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// Type of location (e.g., "city", "village", "tourism").
    /// </summary>
    public string? LocationType { get; init; }

    /// <summary>
    /// Creates a successful geocoding result.
    /// </summary>
    public static GeocodingResult Successful(
        double latitude,
        double longitude,
        string displayName,
        string shortName,
        string? street = null,
        string? neighborhood = null,
        string? city = null,
        string? county = null,
        string? state = null,
        string? postalCode = null,
        string? country = null,
        string? countryCode = null,
        string? locationType = null) => new()
    {
        Success = true,
        Latitude = latitude,
        Longitude = longitude,
        DisplayName = displayName,
        ShortName = shortName,
        Street = street,
        Neighborhood = neighborhood,
        City = city,
        County = county,
        State = state,
        PostalCode = postalCode,
        Country = country,
        CountryCode = countryCode,
        LocationType = locationType
    };

    /// <summary>
    /// Creates a failed geocoding result.
    /// </summary>
    public static GeocodingResult Failed(double latitude, double longitude, string errorMessage) => new()
    {
        Success = false,
        Latitude = latitude,
        Longitude = longitude,
        ErrorMessage = errorMessage
    };
}
