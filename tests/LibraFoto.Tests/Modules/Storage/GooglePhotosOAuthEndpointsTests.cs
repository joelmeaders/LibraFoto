using System.Reflection;
using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    public class GooglePhotosOAuthEndpointsTests
    {
        [Test]
        public async Task GetAuthorizationUrl_IncludesPickerScope()
        {
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;

            await using var dbContext = new LibraFotoDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var provider = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.GooglePhotos,
                Name = "Test Google Photos",
                IsEnabled = false,
                Configuration = null
            };

            dbContext.StorageProviders.Add(provider);
            await dbContext.SaveChangesAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GooglePhotos:ClientId"] = "test-client-id",
                    ["GooglePhotos:ClientSecret"] = "test-client-secret",
                    ["GooglePhotos:RedirectUri"] = "http://localhost:4200/oauth/callback"
                })
                .Build();

            var method = typeof(GooglePhotosOAuthEndpoints)
                .GetMethod("GetAuthorizationUrl", BindingFlags.NonPublic | BindingFlags.Static);

            await Assert.That(method).IsNotNull();

            var task = (Task<Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>>)method!
                .Invoke(null, new object[] { provider.Id, dbContext, configuration, CancellationToken.None })!;

            var result = await task;
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;

            await Assert.That(ok).IsNotNull();
            await Assert.That(ok!.Value).IsNotNull();

            var authUrl = ok.Value!.AuthorizationUrl;
            var query = QueryHelpers.ParseQuery(new Uri(authUrl).Query);
            var scopeValue = query["scope"].ToString();
            var scopes = scopeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            await Assert.That(scopes).Contains("https://www.googleapis.com/auth/photospicker.mediaitems.readonly");
        }
    }
}
