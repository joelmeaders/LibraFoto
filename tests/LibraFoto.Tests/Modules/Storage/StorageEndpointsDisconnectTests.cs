using System.Reflection;
using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage;

public class StorageEndpointsDisconnectTests
{
    [Test]
    public async Task DisconnectProvider_ClearsTokensAndDisablesProvider()
    {
        var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new LibraFotoDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var config = new GooglePhotosConfiguration
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RefreshToken = "refresh-token",
            AccessToken = "access-token",
            AccessTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var entity = new StorageProvider
        {
            Id = 42,
            Type = StorageProviderType.GooglePhotos,
            Name = "Google Photos",
            IsEnabled = true,
            Configuration = JsonSerializer.Serialize(config)
        };

        dbContext.StorageProviders.Add(entity);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());
        var cacheService = Substitute.For<ICacheService>();

        var provider = new GooglePhotosProvider(
            NullLogger<GooglePhotosProvider>.Instance,
            httpClientFactory,
            cacheService,
            dbContext);

        var factory = Substitute.For<IStorageProviderFactory>();
        factory.GetProviderAsync(entity.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IStorageProvider?>(provider));

        var method = typeof(StorageEndpoints)
            .GetMethod("DisconnectProvider", BindingFlags.NonPublic | BindingFlags.Static);

        await Assert.That(method).IsNotNull();

        var task = (Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>, BadRequest<ApiError>>>)method!
            .Invoke(null, new object[]
            {
                entity.Id,
                dbContext,
                factory,
                NullLoggerFactory.Instance,
                CancellationToken.None
            })!;

        var result = await task;
        var ok = result.Result as Ok<StorageProviderDto>;

        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value!.IsEnabled).IsFalse();

        var updated = await dbContext.StorageProviders.FindAsync(entity.Id);
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.IsEnabled).IsFalse();

        var updatedConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(updated.Configuration!);
        await Assert.That(updatedConfig).IsNotNull();
        await Assert.That(updatedConfig!.RefreshToken).IsNull();
        await Assert.That(updatedConfig.AccessToken).IsNull();
        await Assert.That(updatedConfig.AccessTokenExpiry).IsNull();
    }
}
