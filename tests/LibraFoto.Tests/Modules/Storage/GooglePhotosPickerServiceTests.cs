using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraFoto.Modules.Storage.Services;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage;

public class GooglePhotosPickerServiceTests
{
    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient(handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("pageToken=token2", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        mediaItems = new[]
                        {
                            new { id = "item-2", type = "PHOTO", mediaFile = new { baseUrl = "https://example.com/2", mimeType = "image/jpeg" } }
                        }
                    }, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    mediaItems = new[]
                    {
                        new { id = "item-1", type = "PHOTO", mediaFile = new { baseUrl = "https://example.com/1", mimeType = "image/jpeg" } }
                    },
                    nextPageToken = "token2"
                }, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        }
    }

    [Test]
    public async Task ListMediaItemsAsync_HandlesPagination()
    {
        var service = new GooglePhotosPickerService(new TestHttpClientFactory(new StubHandler()));

        var items = await service.ListMediaItemsAsync("session", "token", CancellationToken.None);

        await Assert.That(items).Count().IsEqualTo(2);
        await Assert.That(items[0].Id).IsEqualTo("item-1");
        await Assert.That(items[1].Id).IsEqualTo("item-2");
    }

    [Test]
    public async Task BuildDownloadUrl_UsesVideoDownloadForVideos()
    {
        var url = GooglePhotosPickerService.BuildDownloadUrl("https://example.com/base", true, 2048, 2048);

        await Assert.That(url).IsEqualTo("https://example.com/base=dv");
    }
}
