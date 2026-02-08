using System.Net;
using System.Text.Json;
using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media
{
    public class GeocodingServiceTests
    {
        private GeocodingService _service = null!;
        private MockHttpMessageHandler _mockHandler = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _mockHandler = new MockHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                }));

            var httpClient = new HttpClient(_mockHandler)
            {
                BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
            };

            _service = new GeocodingService(httpClient);
            await Task.CompletedTask;
        }

        #region ReverseGeocodeAsync Tests

        [Test]
        public async Task ReverseGeocodeAsync_SuccessfulResponse_ReturnsPopulatedResult()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "City Hall, Paris, Île-de-France, France",
                address = new
                {
                    city = "Paris",
                    state = "Île-de-France",
                    country = "France",
                    country_code = "fr"
                }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(48.8566, 2.3522);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Latitude).IsEqualTo(48.8566);
            await Assert.That(result.Longitude).IsEqualTo(2.3522);
            await Assert.That(result.DisplayName).IsEqualTo("Paris, Île-de-France, France");
            await Assert.That(result.City).IsEqualTo("Paris");
            await Assert.That(result.State).IsEqualTo("Île-de-France");
            await Assert.That(result.Country).IsEqualTo("France");
            await Assert.That(result.CountryCode).IsEqualTo("FR");
        }

        [Test]
        public async Task ReverseGeocodeAsync_NonSuccessStatusCode_ReturnsEmptyResult()
        {
            // Arrange
            SetupMockResponse(HttpStatusCode.InternalServerError, "Server Error");

            // Act
            var result = await _service.ReverseGeocodeAsync(48.8566, 2.3522);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Latitude).IsEqualTo(48.8566);
            await Assert.That(result.Longitude).IsEqualTo(2.3522);
            await Assert.That(result.DisplayName).IsNull();
            await Assert.That(result.City).IsNull();
            await Assert.That(result.State).IsNull();
            await Assert.That(result.Country).IsNull();
            await Assert.That(result.CountryCode).IsNull();
        }

        [Test]
        public async Task ReverseGeocodeAsync_NullResponseBody_ReturnsEmptyResult()
        {
            // Arrange
            SetupMockResponse(HttpStatusCode.OK, "null");

            // Act
            var result = await _service.ReverseGeocodeAsync(40.7128, -74.0060);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Latitude).IsEqualTo(40.7128);
            await Assert.That(result.Longitude).IsEqualTo(-74.0060);
            await Assert.That(result.DisplayName).IsNull();
            await Assert.That(result.City).IsNull();
        }

        [Test]
        public async Task ReverseGeocodeAsync_ExceptionThrown_ReturnsEmptyResult()
        {
            // Arrange - handler throws an exception to simulate network error
            _mockHandler = new MockHttpMessageHandler(_ =>
                throw new HttpRequestException("Network error"));

            var httpClient = new HttpClient(_mockHandler)
            {
                BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
            };
            _service = new GeocodingService(httpClient);

            // Act
            var result = await _service.ReverseGeocodeAsync(51.5074, -0.1278);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Latitude).IsEqualTo(51.5074);
            await Assert.That(result.Longitude).IsEqualTo(-0.1278);
            await Assert.That(result.DisplayName).IsNull();
            await Assert.That(result.City).IsNull();
        }

        [Test]
        public async Task ReverseGeocodeAsync_TownInsteadOfCity_UsesTownAsCity()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Town Hall, Smallville, Kansas, United States",
                address = new
                {
                    town = "Smallville",
                    state = "Kansas",
                    country = "United States",
                    country_code = "us"
                }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(39.0119, -98.4842);

            // Assert
            await Assert.That(result.City).IsEqualTo("Smallville");
            await Assert.That(result.DisplayName).IsEqualTo("Smallville, Kansas, United States");
            await Assert.That(result.State).IsEqualTo("Kansas");
            await Assert.That(result.Country).IsEqualTo("United States");
            await Assert.That(result.CountryCode).IsEqualTo("US");
        }

        [Test]
        public async Task ReverseGeocodeAsync_VillageInsteadOfCity_UsesVillageAsCity()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Church, Cotswolds, Gloucestershire, England, United Kingdom",
                address = new
                {
                    village = "Cotswolds",
                    state = "England",
                    country = "United Kingdom",
                    country_code = "gb"
                }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(51.8330, -1.8433);

            // Assert
            await Assert.That(result.City).IsEqualTo("Cotswolds");
            await Assert.That(result.DisplayName).IsEqualTo("Cotswolds, England, United Kingdom");
            await Assert.That(result.CountryCode).IsEqualTo("GB");
        }

        [Test]
        public async Task ReverseGeocodeAsync_RequestUrlContainsCorrectParameters()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;

            _mockHandler = new MockHttpMessageHandler(request =>
            {
                capturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        display_name = "Test",
                        address = new { city = "Test", state = "Test", country = "Test", country_code = "xx" }
                    }))
                });
            });

            var httpClient = new HttpClient(_mockHandler)
            {
                BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
            };
            _service = new GeocodingService(httpClient);

            // Act
            await _service.ReverseGeocodeAsync(48.8566, 2.3522);

            // Assert
            await Assert.That(capturedRequest).IsNotNull();
            var requestUrl = capturedRequest!.RequestUri!.ToString();
            await Assert.That(requestUrl).Contains("reverse?format=jsonv2");
            await Assert.That(requestUrl).Contains("lat=48.8566");
            await Assert.That(requestUrl).Contains("lon=2.3522");
        }

        #endregion

        #region GetLocationNameAsync Tests

        [Test]
        public async Task GetLocationNameAsync_SuccessfulGeocode_ReturnsDisplayName()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "City Hall, Paris, Île-de-France, France",
                address = new
                {
                    city = "Paris",
                    state = "Île-de-France",
                    country = "France",
                    country_code = "fr"
                }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var locationName = await _service.GetLocationNameAsync(48.8566, 2.3522);

            // Assert
            await Assert.That(locationName).IsEqualTo("Paris, Île-de-France, France");
        }

        [Test]
        public async Task GetLocationNameAsync_EmptyGeocode_ReturnsNull()
        {
            // Arrange
            SetupMockResponse(HttpStatusCode.InternalServerError, "Error");

            // Act
            var locationName = await _service.GetLocationNameAsync(48.8566, 2.3522);

            // Assert
            await Assert.That(locationName).IsNull();
        }

        #endregion

        #region BatchReverseGeocodeAsync Tests

        [Test]
        public async Task BatchReverseGeocodeAsync_MultipleCoordinates_YieldsResultForEach()
        {
            // Arrange
            var callCount = 0;
            var responses = new[]
            {
                new { display_name = "Paris, France", address = new { city = "Paris", state = "Île-de-France", country = "France", country_code = "fr" } },
                new { display_name = "London, UK", address = new { city = "London", state = "England", country = "United Kingdom", country_code = "gb" } },
                new { display_name = "Berlin, Germany", address = new { city = "Berlin", state = "Berlin", country = "Germany", country_code = "de" } }
            };

            _mockHandler = new MockHttpMessageHandler(_ =>
            {
                var index = callCount < responses.Length ? callCount : responses.Length - 1;
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responses[index]))
                });
            });

            var httpClient = new HttpClient(_mockHandler)
            {
                BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
            };
            _service = new GeocodingService(httpClient);

            var coordinates = new[]
            {
                (48.8566, 2.3522),
                (51.5074, -0.1278),
                (52.5200, 13.4050)
            };

            // Act
            var results = new List<GeocodingResult>();
            await foreach (var result in _service.BatchReverseGeocodeAsync(coordinates))
            {
                results.Add(result);
            }

            // Assert
            await Assert.That(results.Count).IsEqualTo(3);
            await Assert.That(results[0].City).IsEqualTo("Paris");
            await Assert.That(results[1].City).IsEqualTo("London");
            await Assert.That(results[2].City).IsEqualTo("Berlin");
        }

        [Test]
        public async Task BatchReverseGeocodeAsync_CancellationRequested_StopsYielding()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Test Place",
                address = new { city = "Test", state = "State", country = "Country", country_code = "xx" }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            var coordinates = new[]
            {
                (48.8566, 2.3522),
                (51.5074, -0.1278),
                (52.5200, 13.4050),
                (35.6762, 139.6503)
            };

            using var cts = new CancellationTokenSource();

            // Act
            var results = new List<GeocodingResult>();
            var count = 0;
            await foreach (var result in _service.BatchReverseGeocodeAsync(coordinates, cts.Token))
            {
                results.Add(result);
                count++;
                if (count >= 2)
                {
                    cts.Cancel();
                }
            }

            // Assert - should have stopped after 2 results due to cancellation
            await Assert.That(results.Count).IsEqualTo(2);
        }

        #endregion

        #region IsRateLimited Tests

        [Test]
        public async Task IsRateLimited_Initially_ReturnsFalse()
        {
            // The service starts with _lastRequestTime = DateTime.MinValue,
            // so timeSinceLastRequest is very large and _requestsThisMinute is 0
            var isLimited = _service.IsRateLimited;

            await Assert.That(isLimited).IsFalse();
        }

        #endregion

        #region GetRateLimitStatus Tests

        [Test]
        public async Task GetRateLimitStatus_Initially_ReturnsCorrectValues()
        {
            // Act
            var (used, remaining, _) = _service.GetRateLimitStatus();

            // Assert
            await Assert.That(used).IsEqualTo(0);
            await Assert.That(remaining).IsEqualTo(60);
        }

        [Test]
        public async Task GetRateLimitStatus_AfterRequest_IncrementsUsedCount()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Test",
                address = new { city = "Test", state = "State", country = "Country", country_code = "xx" }
            });
            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            await _service.ReverseGeocodeAsync(48.8566, 2.3522);
            var (used, remaining, _) = _service.GetRateLimitStatus();

            // Assert
            await Assert.That(used).IsEqualTo(1);
            await Assert.That(remaining).IsEqualTo(59);
        }

        #endregion

        #region FormatDisplayName Tests

        [Test]
        public async Task ReverseGeocodeAsync_NoAddressParts_FallsBackToResponseDisplayName()
        {
            // Arrange - response has display_name but no address parts (city/town/village/state/country)
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Middle of Nowhere, Ocean",
                address = new { }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(0.0, 0.0);

            // Assert - should fall back to the raw display_name from the API
            await Assert.That(result.DisplayName).IsEqualTo("Middle of Nowhere, Ocean");
        }

        [Test]
        public async Task ReverseGeocodeAsync_NullAddress_FallsBackToResponseDisplayName()
        {
            // Arrange - response has display_name but null address
            var json = """{"display_name": "Somewhere on Earth", "address": null}""";

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(10.0, 20.0);

            // Assert
            await Assert.That(result.DisplayName).IsEqualTo("Somewhere on Earth");
        }

        [Test]
        public async Task ReverseGeocodeAsync_OnlyStateAndCountry_FormatsWithoutCity()
        {
            // Arrange - no city/town/village, but has state and country
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Highway 1, California, United States",
                address = new
                {
                    state = "California",
                    country = "United States",
                    country_code = "us"
                }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(36.2388, -121.8194);

            // Assert - DisplayName built from state + country (no city part)
            await Assert.That(result.DisplayName).IsEqualTo("California, United States");
            await Assert.That(result.City).IsNull();
            await Assert.That(result.State).IsEqualTo("California");
            await Assert.That(result.Country).IsEqualTo("United States");
        }

        [Test]
        public async Task ReverseGeocodeAsync_OnlyCountry_FormatsWithCountryOnly()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Antarctica",
                address = new
                {
                    country = "Antarctica",
                    country_code = "aq"
                }
            });

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(-82.8628, 135.0000);

            // Assert
            await Assert.That(result.DisplayName).IsEqualTo("Antarctica");
            await Assert.That(result.CountryCode).IsEqualTo("AQ");
        }

        [Test]
        public async Task ReverseGeocodeAsync_CityAndTownBothPresent_PrefersCityOverTown()
        {
            // Arrange - when both city and town are present, city takes precedence
            var json = """
            {
                "display_name": "Some Place",
                "address": {
                    "city": "Metro City",
                    "town": "Small Town",
                    "state": "State",
                    "country": "Country",
                    "country_code": "xx"
                }
            }
            """;

            SetupMockResponse(HttpStatusCode.OK, json);

            // Act
            var result = await _service.ReverseGeocodeAsync(10.0, 20.0);

            // Assert - city should be preferred
            await Assert.That(result.City).IsEqualTo("Metro City");
            await Assert.That(result.DisplayName).IsEqualTo("Metro City, State, Country");
        }

        #endregion

        #region Constructor Tests

        [Test]
        public async Task Constructor_WithNullHttpClient_CreatesDefaultClient()
        {
            // Act - should not throw
            var service = new GeocodingService(null);

            // Assert - service is created and initially not rate limited
            await Assert.That(service.IsRateLimited).IsFalse();
        }

        [Test]
        public async Task Constructor_WithCustomHttpClient_UsesProvidedClient()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                display_name = "Custom Client Test",
                address = new { city = "Custom", state = "Test", country = "Land", country_code = "ct" }
            });

            var handler = new MockHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                }));

            var customClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
            };

            var service = new GeocodingService(customClient);

            // Act
            var result = await service.ReverseGeocodeAsync(1.0, 2.0);

            // Assert - verifies the custom client was used
            await Assert.That(result.City).IsEqualTo("Custom");
        }

        #endregion

        #region Helpers

        private void SetupMockResponse(HttpStatusCode statusCode, string content)
        {
            _mockHandler = new MockHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                }));

            var httpClient = new HttpClient(_mockHandler)
            {
                BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
            };

            _service = new GeocodingService(httpClient);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }

        #endregion
    }
}
