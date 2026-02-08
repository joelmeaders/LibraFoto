using System.Reflection;
using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    public class StorageEndpointsStatusMessageTests
    {
        [Test]
        public async Task GetProvider_ReturnsScopeWarning_WhenGrantedScopesMissing()
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
                GrantedScopes = ["https://www.googleapis.com/auth/photoslibrary.readonly"]
            };

            var entity = new StorageProvider
            {
                Id = 7,
                Type = StorageProviderType.GooglePhotos,
                Name = "Google Photos",
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };

            dbContext.StorageProviders.Add(entity);
            await dbContext.SaveChangesAsync();

            var provider = Substitute.For<IStorageProvider>();
            provider.TestConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

            var factory = Substitute.For<IStorageProviderFactory>();
            factory.GetProviderAsync(entity.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IStorageProvider?>(provider));

            var method = typeof(StorageEndpoints)
                .GetMethod("GetProvider", BindingFlags.NonPublic | BindingFlags.Static);

            await Assert.That(method).IsNotNull();

            var task = (Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>>>)method!
                .Invoke(null, new object[] { entity.Id, dbContext, factory, CancellationToken.None })!;

            var result = await task;
            var ok = result.Result as Ok<StorageProviderDto>;

            await Assert.That(ok).IsNotNull();
            await Assert.That(ok!.Value!.StatusMessage).IsNotNull();
            await Assert.That(ok.Value!.StatusMessage!).Contains("photospicker.mediaitems.readonly");
        }
    }
}
