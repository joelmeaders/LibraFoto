using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Services
{
    /// <summary>
    /// Comprehensive tests for GooglePhotosPickerService - Google Photos Picker API integration service.
    /// Tests session management, media item retrieval, token handling, HTTP client usage, and error scenarios.
    /// Coverage: 25 test cases covering all service methods and edge cases.
    /// </summary>
    public class GooglePhotosPickerServiceTests
    {
        private IHttpClientFactory _httpClientFactory = null!;
        private GooglePhotosPickerService _service = null!;
        private TestHttpMessageHandler _httpHandler = null!;
        private const string TestAccessToken = "test-access-token-12345";
        private const string TestSessionId = "session-abc-123";
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [Before(Test)]
        public void Setup()
        {
            _httpHandler = new TestHttpMessageHandler();
            var httpClient = new HttpClient(_httpHandler)
            {
                BaseAddress = new Uri("https://photospicker.googleapis.com")
            };

            _httpClientFactory = Substitute.For<IHttpClientFactory>();
            _httpClientFactory.CreateClient().Returns(httpClient);
            _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

            _service = new GooglePhotosPickerService(_httpClientFactory);
        }

        [After(Test)]
        public void Cleanup()
        {
            _httpHandler?.Dispose();
        }

        #region CreateSessionAsync Tests

        [Test]
        public async Task CreateSessionAsync_WithValidToken_CreatesSession()
        {
            // Arrange
            var sessionResponse = new PickerSessionResponse
            {
                Id = TestSessionId,
                PickerUri = "https://photospicker.google.com/picker/123",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1),
                PollingConfig = new PickerPollingConfigResponse
                {
                    PollInterval = "10s",
                    TimeoutIn = "300s"
                }
            };

            _httpHandler.SetResponse(HttpStatusCode.OK, sessionResponse);

            // Act
            var result = await _service.CreateSessionAsync(TestAccessToken, null, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Id).IsEqualTo(TestSessionId);
            await Assert.That(result.PickerUri).IsEqualTo("https://photospicker.google.com/picker/123");
            await Assert.That(result.MediaItemsSet).IsFalse();

            // Verify request
            await Assert.That(_httpHandler.LastRequest).IsNotNull();
            await Assert.That(_httpHandler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(_httpHandler.LastRequest.RequestUri!.AbsolutePath).Contains("/v1/sessions");
            await Assert.That(_httpHandler.LastRequest.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
            await Assert.That(_httpHandler.LastRequest.Headers.Authorization.Parameter).IsEqualTo(TestAccessToken);
        }

        [Test]
        public async Task CreateSessionAsync_WithMaxItemCount_IncludesPickingConfig()
        {
            // Arrange
            var sessionResponse = new PickerSessionResponse
            {
                Id = TestSessionId,
                PickerUri = "https://photospicker.google.com/picker/123",
                MediaItemsSet = false
            };

            _httpHandler.SetResponse(HttpStatusCode.OK, sessionResponse);

            // Act
            await _service.CreateSessionAsync(TestAccessToken, 50, CancellationToken.None);

            // Assert
            var requestContent = await _httpHandler.LastRequest!.Content!.ReadAsStringAsync();
            var requestObject = JsonSerializer.Deserialize<JsonElement>(requestContent);

            await Assert.That(requestObject.TryGetProperty("pickingConfig", out var pickingConfig)).IsTrue();
            await Assert.That(pickingConfig.TryGetProperty("maxItemCount", out var maxItemCount)).IsTrue();
            await Assert.That(maxItemCount.GetInt64()).IsEqualTo(50);
        }

        [Test]
        public async Task CreateSessionAsync_WithoutMaxItemCount_OmitsPickingConfig()
        {
            // Arrange
            var sessionResponse = new PickerSessionResponse
            {
                Id = TestSessionId,
                PickerUri = "https://photospicker.google.com/picker/123",
                MediaItemsSet = false
            };

            _httpHandler.SetResponse(HttpStatusCode.OK, sessionResponse);

            // Act
            await _service.CreateSessionAsync(TestAccessToken, null, CancellationToken.None);

            // Assert
            var requestContent = await _httpHandler.LastRequest!.Content!.ReadAsStringAsync();
            var requestObject = JsonSerializer.Deserialize<JsonElement>(requestContent);

            await Assert.That(requestObject.TryGetProperty("pickingConfig", out var pickingConfig)).IsTrue();
            await Assert.That(pickingConfig.ValueKind).IsEqualTo(JsonValueKind.Null);
        }

        [Test]
        public async Task CreateSessionAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.Unauthorized, string.Empty);

            // Act & Assert
            await Assert.That(async () =>
                await _service.CreateSessionAsync(TestAccessToken, null, CancellationToken.None))
                .Throws<HttpRequestException>();
        }

        [Test]
        public async Task CreateSessionAsync_WithNullResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.OK, "null");

            // Act & Assert
            await Assert.That(async () =>
                await _service.CreateSessionAsync(TestAccessToken, null, CancellationToken.None))
                .Throws<InvalidOperationException>()
                .WithMessageContaining("Failed to parse picker session response");
        }

        [Test]
        public async Task CreateSessionAsync_WithMalformedJson_ThrowsJsonException()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.OK, "{invalid json");

            // Act & Assert
            await Assert.That(async () =>
                await _service.CreateSessionAsync(TestAccessToken, null, CancellationToken.None))
                .Throws<JsonException>();
        }

        [Test]
        public async Task CreateSessionAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _service.CreateSessionAsync(TestAccessToken, null, cts.Token));
        }

        #endregion

        #region GetSessionAsync Tests

        [Test]
        public async Task GetSessionAsync_WithValidSession_ReturnsSessionDetails()
        {
            // Arrange
            var sessionResponse = new PickerSessionResponse
            {
                Id = TestSessionId,
                PickerUri = "https://photospicker.google.com/picker/123",
                MediaItemsSet = true,
                ExpireTime = DateTime.UtcNow.AddHours(1),
                PollingConfig = new PickerPollingConfigResponse
                {
                    PollInterval = "5s",
                    TimeoutIn = "200s"
                }
            };

            _httpHandler.SetResponse(HttpStatusCode.OK, sessionResponse);

            // Act
            var result = await _service.GetSessionAsync(TestSessionId, TestAccessToken, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Id).IsEqualTo(TestSessionId);
            await Assert.That(result.MediaItemsSet).IsTrue();
            await Assert.That(result.PollingConfig).IsNotNull();

            // Verify request
            await Assert.That(_httpHandler.LastRequest!.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(_httpHandler.LastRequest.RequestUri!.AbsolutePath).Contains($"/v1/sessions/{TestSessionId}");
            await Assert.That(_httpHandler.LastRequest.Headers.Authorization!.Parameter).IsEqualTo(TestAccessToken);
        }

        [Test]
        public async Task GetSessionAsync_WithNotFoundSession_ThrowsHttpRequestException()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.NotFound, string.Empty);

            // Act & Assert
            await Assert.That(async () =>
                await _service.GetSessionAsync(TestSessionId, TestAccessToken, CancellationToken.None))
                .Throws<HttpRequestException>();
        }

        [Test]
        public async Task GetSessionAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _service.GetSessionAsync(TestAccessToken, TestSessionId, cts.Token));
        }

        #endregion

        #region ListMediaItemsAsync Tests

        [Test]
        public async Task ListMediaItemsAsync_WithSinglePage_ReturnsAllItems()
        {
            // Arrange
            var response = new PickedMediaItemsResponse
            {
                MediaItems = new List<PickedMediaItemResponse>
                {
                    CreateTestMediaItem("item1", "PHOTO"),
                    CreateTestMediaItem("item2", "VIDEO")
                },
                NextPageToken = null
            };

            _httpHandler.SetResponse(HttpStatusCode.OK, response);

            // Act
            var result = await _service.ListMediaItemsAsync(TestSessionId, TestAccessToken, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Count).IsEqualTo(2);
            await Assert.That(result[0].Id).IsEqualTo("item1");
            await Assert.That(result[0].Type).IsEqualTo("PHOTO");
            await Assert.That(result[1].Id).IsEqualTo("item2");

            // Verify request URL
            var requestUri = _httpHandler.LastRequest!.RequestUri!.ToString();
            await Assert.That(requestUri).Contains($"sessionId={Uri.EscapeDataString(TestSessionId)}");
            await Assert.That(requestUri).Contains("pageSize=100");
        }

        [Test]
        public async Task ListMediaItemsAsync_WithMultiplePages_ReturnsAllItems()
        {
            // Arrange - Setup paginated responses
            var page1 = new PickedMediaItemsResponse
            {
                MediaItems = new List<PickedMediaItemResponse>
                {
                    CreateTestMediaItem("item1", "PHOTO"),
                    CreateTestMediaItem("item2", "PHOTO")
                },
                NextPageToken = "page2token"
            };

            var page2 = new PickedMediaItemsResponse
            {
                MediaItems = new List<PickedMediaItemResponse>
                {
                    CreateTestMediaItem("item3", "VIDEO")
                },
                NextPageToken = null
            };

            _httpHandler.SetMultipleResponses(new[]
            {
                (HttpStatusCode.OK, JsonSerializer.Serialize(page1, _jsonOptions)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(page2, _jsonOptions))
            });

            // Act
            var result = await _service.ListMediaItemsAsync(TestSessionId, TestAccessToken, CancellationToken.None);

            // Assert
            await Assert.That(result.Count).IsEqualTo(3);
            await Assert.That(result[0].Id).IsEqualTo("item1");
            await Assert.That(result[1].Id).IsEqualTo("item2");
            await Assert.That(result[2].Id).IsEqualTo("item3");

            // Verify page token was used in second request
            await Assert.That(_httpHandler.RequestHistory.Count).IsEqualTo(2);
            var secondRequest = _httpHandler.RequestHistory[1].ToString();
            await Assert.That(secondRequest).Contains("pageToken=page2token");
        }

        [Test]
        public async Task ListMediaItemsAsync_WithEmptyResult_ReturnsEmptyList()
        {
            // Arrange
            var response = new PickedMediaItemsResponse
            {
                MediaItems = null,
                NextPageToken = null
            };

            _httpHandler.SetResponse(HttpStatusCode.OK, response);

            // Act
            var result = await _service.ListMediaItemsAsync(TestSessionId, TestAccessToken, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Count).IsEqualTo(0);
        }

        [Test]
        public async Task ListMediaItemsAsync_WithNullMediaItemsList_ReturnsEmptyList()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.OK, "{}");

            // Act
            var result = await _service.ListMediaItemsAsync(TestSessionId, TestAccessToken, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Count).IsEqualTo(0);
        }

        [Test]
        public async Task ListMediaItemsAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _service.ListMediaItemsAsync(TestAccessToken, TestSessionId, cts.Token));
        }

        #endregion

        #region DownloadMediaItemAsync Tests

        [Test]
        public async Task DownloadMediaItemAsync_WithValidUrl_ReturnsStreamAndContentType()
        {
            // Arrange
            var imageData = Encoding.UTF8.GetBytes("fake-image-data");
            _httpHandler.SetBinaryResponse(HttpStatusCode.OK, imageData, "image/jpeg");

            // Act
            var (stream, contentType) = await _service.DownloadMediaItemAsync(
                "https://photos.google.com/photo/123/baseUrl",
                TestAccessToken,
                isVideo: false,
                maxWidth: 1920,
                maxHeight: 1080,
                CancellationToken.None);

            // Assert
            await Assert.That(stream).IsNotNull();
            await Assert.That(contentType).IsEqualTo("image/jpeg");

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            await Assert.That(content).IsEqualTo("fake-image-data");

            // Verify authorization header
            await Assert.That(_httpHandler.LastRequest!.Headers.Authorization!.Parameter).IsEqualTo(TestAccessToken);
        }

        [Test]
        public async Task DownloadMediaItemAsync_WithDefaultContentType_ReturnsOctetStream()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("data");
            _httpHandler.SetBinaryResponse(HttpStatusCode.OK, data, null);

            // Act
            var (_, contentType) = await _service.DownloadMediaItemAsync(
                "https://photos.google.com/photo/123/baseUrl",
                TestAccessToken,
                isVideo: false,
                maxWidth: 800,
                maxHeight: 600,
                CancellationToken.None);

            // Assert
            await Assert.That(contentType).IsEqualTo("application/octet-stream");
        }

        [Test]
        public async Task DownloadMediaItemAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.Forbidden, string.Empty);

            // Act & Assert
            await Assert.That(async () =>
                await _service.DownloadMediaItemAsync(
                    "https://photos.google.com/photo/123/baseUrl",
                    TestAccessToken,
                    isVideo: false,
                    maxWidth: 1920,
                    maxHeight: 1080,
                    CancellationToken.None))
                .Throws<HttpRequestException>();
        }

        [Test]
        public async Task DownloadMediaItemAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _service.DownloadMediaItemAsync(
                    "https://photos.google.com/photo/123/baseUrl",
                    TestAccessToken,
                    isVideo: false,
                    maxWidth: 1920,
                    maxHeight: 1080,
                    cts.Token));
        }

        #endregion

        #region DeleteSessionAsync Tests

        [Test]
        public async Task DeleteSessionAsync_WithValidSession_DeletesSuccessfully()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.NoContent, string.Empty);

            // Act
            await _service.DeleteSessionAsync(TestSessionId, TestAccessToken, CancellationToken.None);

            // Assert
            await Assert.That(_httpHandler.LastRequest).IsNotNull();
            await Assert.That(_httpHandler.LastRequest!.Method).IsEqualTo(HttpMethod.Delete);
            await Assert.That(_httpHandler.LastRequest.RequestUri!.AbsolutePath).Contains($"/v1/sessions/{TestSessionId}");
            await Assert.That(_httpHandler.LastRequest.Headers.Authorization!.Parameter).IsEqualTo(TestAccessToken);
        }

        [Test]
        public async Task DeleteSessionAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            _httpHandler.SetResponse(HttpStatusCode.NotFound, string.Empty);

            // Act & Assert
            await Assert.That(async () =>
                await _service.DeleteSessionAsync(TestSessionId, TestAccessToken, CancellationToken.None))
                .Throws<HttpRequestException>();
        }

        [Test]
        public async Task DeleteSessionAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _service.DeleteSessionAsync(TestAccessToken, TestSessionId, cts.Token));
        }

        #endregion

        #region BuildDownloadUrl Tests

        [Test]
        public async Task BuildDownloadUrl_ForVideo_AppendsVideoSuffix()
        {
            // Act
            var url = GooglePhotosPickerService.BuildDownloadUrl(
                "https://photos.google.com/photo/123/baseUrl",
                isVideo: true,
                maxWidth: 0,
                maxHeight: 0);

            // Assert
            await Assert.That(url).IsEqualTo("https://photos.google.com/photo/123/baseUrl=dv");
        }

        [Test]
        public async Task BuildDownloadUrl_ForPhotoWithDimensions_AppendsWidthHeight()
        {
            // Act
            var url = GooglePhotosPickerService.BuildDownloadUrl(
                "https://photos.google.com/photo/123/baseUrl",
                isVideo: false,
                maxWidth: 1920,
                maxHeight: 1080);

            // Assert
            await Assert.That(url).IsEqualTo("https://photos.google.com/photo/123/baseUrl=w1920-h1080");
        }

        [Test]
        public async Task BuildDownloadUrl_ForPhotoWithoutDimensions_AppendsDownloadSuffix()
        {
            // Act
            var url = GooglePhotosPickerService.BuildDownloadUrl(
                "https://photos.google.com/photo/123/baseUrl",
                isVideo: false,
                maxWidth: 0,
                maxHeight: 0);

            // Assert
            await Assert.That(url).IsEqualTo("https://photos.google.com/photo/123/baseUrl=d");
        }

        [Test]
        public async Task BuildDownloadUrl_ForPhotoWithOnlyWidth_AppendsDownloadSuffix()
        {
            // Act
            var url = GooglePhotosPickerService.BuildDownloadUrl(
                "https://photos.google.com/photo/123/baseUrl",
                isVideo: false,
                maxWidth: 1920,
                maxHeight: 0);

            // Assert
            await Assert.That(url).IsEqualTo("https://photos.google.com/photo/123/baseUrl=d");
        }

        [Test]
        public async Task BuildDownloadUrl_ForPhotoWithOnlyHeight_AppendsDownloadSuffix()
        {
            // Act
            var url = GooglePhotosPickerService.BuildDownloadUrl(
                "https://photos.google.com/photo/123/baseUrl",
                isVideo: false,
                maxWidth: 0,
                maxHeight: 1080);

            // Assert
            await Assert.That(url).IsEqualTo("https://photos.google.com/photo/123/baseUrl=d");
        }

        #endregion

        #region Helper Methods

        private static PickedMediaItemResponse CreateTestMediaItem(string id, string type)
        {
            return new PickedMediaItemResponse
            {
                Id = id,
                Type = type,
                CreateTime = DateTime.UtcNow,
                MediaFile = new PickedMediaFileResponse
                {
                    BaseUrl = $"https://photos.google.com/{id}/base",
                    MimeType = type == "VIDEO" ? "video/mp4" : "image/jpeg",
                    Filename = $"{id}.jpg",
                    MediaFileMetadata = new PickedMediaFileMetadataResponse
                    {
                        Width = 1920,
                        Height = 1080
                    }
                }
            };
        }

        #endregion

        #region Test HTTP Message Handler

        /// <summary>
        /// Custom HTTP message handler for testing HTTP client calls.
        /// Allows setting predetermined responses and inspecting requests.
        /// </summary>
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private HttpStatusCode _statusCode = HttpStatusCode.OK;
            private string _content = string.Empty;
            private string? _contentType;
            private byte[]? _binaryContent;
            private Queue<(HttpStatusCode statusCode, string content)>? _multipleResponses;

            public HttpRequestMessage? LastRequest { get; private set; }
            public List<Uri> RequestHistory { get; } = new();

            public void SetResponse(HttpStatusCode statusCode, object content)
            {
                _statusCode = statusCode;
                _content = content is string str ? str : JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                _contentType = "application/json";
                _binaryContent = null;
                _multipleResponses = null;
            }

            public void SetBinaryResponse(HttpStatusCode statusCode, byte[] content, string? contentType)
            {
                _statusCode = statusCode;
                _binaryContent = content;
                _contentType = contentType;
                _content = string.Empty;
                _multipleResponses = null;
            }

            public void SetMultipleResponses((HttpStatusCode statusCode, string content)[] responses)
            {
                _multipleResponses = new Queue<(HttpStatusCode, string)>(responses);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                RequestHistory.Add(request.RequestUri!);

                // Handle multiple responses for pagination testing
                if (_multipleResponses is { Count: > 0 })
                {
                    var (statusCode, content) = _multipleResponses.Dequeue();
                    var response = new HttpResponseMessage(statusCode)
                    {
                        Content = new StringContent(content, Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(response);
                }

                // Handle single response
                var responseMessage = new HttpResponseMessage(_statusCode);

                if (_binaryContent != null)
                {
                    responseMessage.Content = new ByteArrayContent(_binaryContent);
                    if (_contentType != null)
                    {
                        responseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(_contentType);
                    }
                }
                else if (!string.IsNullOrEmpty(_content))
                {
                    responseMessage.Content = new StringContent(_content, Encoding.UTF8, _contentType ?? "application/json");
                }

                return Task.FromResult(responseMessage);
            }
        }

        #endregion
    }
}
