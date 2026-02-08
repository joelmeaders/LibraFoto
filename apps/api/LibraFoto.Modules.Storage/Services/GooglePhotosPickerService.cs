using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Services
{
    /// <summary>
    /// Service for interacting with the Google Photos Picker API.
    /// </summary>
    public class GooglePhotosPickerService
    {
        private const string PickerApiBase = "https://photospicker.googleapis.com/v1";
        private const string AuthorizationScheme = "Bearer";
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public GooglePhotosPickerService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        internal async Task<PickerSessionResponse> CreateSessionAsync(
            string accessToken,
            long? maxItemCount,
            CancellationToken cancellationToken)
        {
            var request = new PickerSessionRequest
            {
                PickingConfig = maxItemCount is > 0 ? new PickerSessionPickingConfig { MaxItemCount = maxItemCount } : null
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{PickerApiBase}/sessions")
            {
                Content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthorizationScheme, accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<PickerSessionResponse>(payload, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to parse picker session response.");
        }

        internal async Task<PickerSessionResponse> GetSessionAsync(
            string sessionId,
            string accessToken,
            CancellationToken cancellationToken)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{PickerApiBase}/sessions/{sessionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthorizationScheme, accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<PickerSessionResponse>(payload, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to parse picker session response.");
        }

        internal async Task<IReadOnlyList<PickedMediaItemResponse>> ListMediaItemsAsync(
            string sessionId,
            string accessToken,
            CancellationToken cancellationToken)
        {
            var items = new List<PickedMediaItemResponse>();
            string? nextPageToken = null;

            do
            {
                var url = new StringBuilder($"{PickerApiBase}/mediaItems?sessionId={Uri.EscapeDataString(sessionId)}&pageSize=100");
                if (!string.IsNullOrWhiteSpace(nextPageToken))
                {
                    url.Append("&pageToken=").Append(Uri.EscapeDataString(nextPageToken));
                }

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url.ToString());
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthorizationScheme, accessToken);

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<PickedMediaItemsResponse>(payload, _jsonOptions)
                    ?? new PickedMediaItemsResponse();

                if (result.MediaItems is { Count: > 0 })
                {
                    items.AddRange(result.MediaItems);
                }

                nextPageToken = result.NextPageToken;
            } while (!string.IsNullOrWhiteSpace(nextPageToken));

            return items;
        }

        public async Task<(Stream Stream, string ContentType)> DownloadMediaItemAsync(
            string baseUrl,
            string accessToken,
            bool isVideo,
            int maxWidth,
            int maxHeight,
            CancellationToken cancellationToken)
        {
            var downloadUrl = BuildDownloadUrl(baseUrl, isVideo, maxWidth, maxHeight);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthorizationScheme, accessToken);

            var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return (memoryStream, contentType);
        }

        public async Task DeleteSessionAsync(
            string sessionId,
            string accessToken,
            CancellationToken cancellationToken)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{PickerApiBase}/sessions/{sessionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthorizationScheme, accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public static string BuildDownloadUrl(string baseUrl, bool isVideo, int maxWidth, int maxHeight)
        {
            if (isVideo)
            {
                return $"{baseUrl}=dv";
            }

            if (maxWidth > 0 && maxHeight > 0)
            {
                return $"{baseUrl}=w{maxWidth}-h{maxHeight}";
            }

            return $"{baseUrl}=d";
        }
    }
}
