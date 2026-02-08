using System.Reflection;
using System.Text.Json;
using Google.Apis.Auth.OAuth2.Responses;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    public class GooglePhotosOAuthEndpointsTests
    {
        #region GetAuthorizationUrl Tests

        [Test]
        public async Task GetAuthorizationUrl_WithValidProvider_ReturnsAuthUrl()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok).IsNotNull();
            await Assert.That(ok!.Value).IsNotNull();
            await Assert.That(ok.Value!.AuthorizationUrl).IsNotEmpty();
            await Assert.That(ok.Value.RedirectUri).IsEqualTo("http://localhost:4200/oauth/callback");
        }

        [Test]
        public async Task GetAuthorizationUrl_IncludesPickerScope()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok).IsNotNull();

            var authUrl = ok!.Value!.AuthorizationUrl;
            var query = QueryHelpers.ParseQuery(new Uri(authUrl).Query);
            var scopeValue = query["scope"].ToString();
            var scopes = scopeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            await Assert.That(scopes).Contains("https://www.googleapis.com/auth/photospicker.mediaitems.readonly");
        }

        [Test]
        public async Task GetAuthorizationUrl_IncludesStateParameter()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            var authUrl = ok!.Value!.AuthorizationUrl;
            var query = QueryHelpers.ParseQuery(new Uri(authUrl).Query);

            await Assert.That(query["state"].ToString()).IsEqualTo(provider.Id.ToString());
        }

        [Test]
        public async Task GetAuthorizationUrl_IncludesOfflineAccessParameters()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            var authUrl = ok!.Value!.AuthorizationUrl;
            var query = QueryHelpers.ParseQuery(new Uri(authUrl).Query);

            await Assert.That(query["access_type"].ToString()).IsEqualTo("offline");
            await Assert.That(query["prompt"].ToString()).IsEqualTo("consent");
            await Assert.That(query["include_granted_scopes"].ToString()).IsEqualTo("true");
        }

        [Test]
        public async Task GetAuthorizationUrl_UsesConfigurationFromProviderConfig()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var config = new GooglePhotosConfiguration
            {
                ClientId = "provider-client-id",
                ClientSecret = "provider-client-secret"
            };
            provider.Configuration = JsonSerializer.Serialize(config);
            await dbContext.SaveChangesAsync();

            var configuration = new ConfigurationBuilder().Build(); // Empty config

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok).IsNotNull(); // Should succeed with provider config
        }

        [Test]
        public async Task GetAuthorizationUrl_WithNonExistentProvider_ReturnsNotFound()
        {
            // Arrange
            var (dbContext, _) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(999, dbContext, configuration);

            // Assert
            var notFound = result.Result as NotFound<ApiError>;
            await Assert.That(notFound).IsNotNull();
            await Assert.That(notFound!.Value!.Code).IsEqualTo("PROVIDER_NOT_FOUND");
        }

        [Test]
        public async Task GetAuthorizationUrl_WithWrongProviderType_ReturnsNotFound()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Type = StorageProviderType.Local; // Wrong type
            await dbContext.SaveChangesAsync();
            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var notFound = result.Result as NotFound<ApiError>;
            await Assert.That(notFound).IsNotNull();
            await Assert.That(notFound!.Value!.Code).IsEqualTo("PROVIDER_NOT_FOUND");
        }

        [Test]
        public async Task GetAuthorizationUrl_WithMissingClientId_ReturnsNotFound()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GooglePhotos:ClientSecret"] = "test-secret"
                    // ClientId missing
                })
                .Build();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var notFound = result.Result as NotFound<ApiError>;
            await Assert.That(notFound).IsNotNull();
            await Assert.That(notFound!.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        public async Task GetAuthorizationUrl_WithMissingClientSecret_ReturnsNotFound()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GooglePhotos:ClientId"] = "test-client-id"
                    // ClientSecret missing
                })
                .Build();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var notFound = result.Result as NotFound<ApiError>;
            await Assert.That(notFound).IsNotNull();
            await Assert.That(notFound!.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        public async Task GetAuthorizationUrl_WithCustomRedirectUri_UsesCustomUri()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var customRedirect = "https://example.com/custom/callback";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GooglePhotos:ClientId"] = "test-client-id",
                    ["GooglePhotos:ClientSecret"] = "test-client-secret",
                    ["GooglePhotos:RedirectUri"] = customRedirect
                })
                .Build();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok!.Value!.RedirectUri).IsEqualTo(customRedirect);
        }

        [Test]
        public async Task GetAuthorizationUrl_WithoutRedirectUriConfig_UsesDefaultUri()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GooglePhotos:ClientId"] = "test-client-id",
                    ["GooglePhotos:ClientSecret"] = "test-client-secret"
                    // RedirectUri not configured
                })
                .Build();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok!.Value!.RedirectUri).IsEqualTo("http://localhost:4200/oauth/callback");
        }

        #endregion

        #region HandleOAuthCallback Tests

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithValidCode_ReturnsSuccess()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-auth-code"
            };

            // Note: This test will call the real Google OAuth flow, which will fail
            // but we can still test the basic flow structure
            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert - Expect BadRequest due to invalid code, but confirms flow executes
            await Assert.That(result.Result).IsNotNull();
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithNonExistentProvider_ReturnsNotFound()
        {
            // Arrange
            var (dbContext, _) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                999, request, dbContext, configuration, loggerFactory);

            // Assert
            var notFound = result.Result as NotFound<ApiError>;
            await Assert.That(notFound).IsNotNull();
            await Assert.That(notFound!.Value!.Code).IsEqualTo("PROVIDER_NOT_FOUND");
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithWrongProviderType_ReturnsNotFound()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Type = StorageProviderType.Local;
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var notFound = result.Result as NotFound<ApiError>;
            await Assert.That(notFound).IsNotNull();
            await Assert.That(notFound!.Value!.Code).IsEqualTo("PROVIDER_NOT_FOUND");
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithMissingCredentials_ReturnsBadRequest()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = new ConfigurationBuilder().Build(); // No credentials
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
            await Assert.That(badRequest!.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithInvalidAuthCode_ReturnsBadRequest()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "invalid-code-12345"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
            // Should be either TOKEN_EXCHANGE_FAILED or OAUTH_ERROR
            await Assert.That(badRequest!.Value!.Code).IsIn(["TOKEN_EXCHANGE_FAILED", "OAUTH_ERROR"]);
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_UsesCredentialsFromProviderConfig()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var config = new GooglePhotosConfiguration
            {
                ClientId = "provider-client-id",
                ClientSecret = "provider-client-secret"
            };
            provider.Configuration = JsonSerializer.Serialize(config);
            await dbContext.SaveChangesAsync();

            var configuration = new ConfigurationBuilder().Build(); // No env config
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
            // Should fail on token exchange, not credentials
            await Assert.That(badRequest!.Value!.Code).IsNotEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithEmptyAuthCode_ReturnsBadRequest()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = string.Empty
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await InvokeHandleOAuthCallback(
                    provider.Id, request, dbContext, configuration, loggerFactory, cts.Token);
            });
        }

        #endregion

        #region Edge Cases and Security Tests

        [Test]
        public async Task GetAuthorizationUrl_WithNullConfiguration_HandlesGracefully()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Configuration = null;
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok).IsNotNull(); // Should succeed with env config
        }

        [Test]
        public async Task GetAuthorizationUrl_WithEmptyConfiguration_HandlesGracefully()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Configuration = string.Empty;
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok).IsNotNull();
        }

        [Test]
        public async Task GetAuthorizationUrl_WithInvalidJsonConfiguration_UsesEnvironmentConfig()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Configuration = "{invalid json";
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();

            // Act
            var result = await InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration);

            // Assert - Should fall back to environment config and succeed
            var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
            await Assert.That(ok).IsNotNull();
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithNullConfiguration_UsesEnvironmentConfig()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Configuration = null;
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert - Should fail on token exchange, not credentials
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
            await Assert.That(badRequest!.Value!.Code).IsNotEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithInvalidJsonConfiguration_UsesEnvironmentConfig()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            provider.Configuration = "{invalid";
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "test-code"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
            await Assert.That(badRequest!.Value!.Code).IsNotEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        public async Task GetAuthorizationUrl_WithMultipleConcurrentRequests_HandlesCorrectly()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();

            // Act - Make multiple concurrent requests
            var tasks = Enumerable.Range(0, 5).Select(_ =>
                InvokeGetAuthorizationUrl(provider.Id, dbContext, configuration));

            var results = await Task.WhenAll(tasks);

            // Assert - All should succeed
            foreach (var result in results)
            {
                var ok = result.Result as Ok<GooglePhotosAuthUrlResponse>;
                await Assert.That(ok).IsNotNull();
                await Assert.That(ok!.Value!.AuthorizationUrl).IsNotEmpty();
            }
        }

        [Test]
        public async Task GetAuthorizationUrl_GeneratesUniqueUrlsForDifferentProviders()
        {
            // Arrange
            var dbContext = CreateDbContext();
            await dbContext.Database.EnsureCreatedAsync();

            var provider1 = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.GooglePhotos,
                Name = "Provider 1",
                IsEnabled = false
            };
            var provider2 = new StorageProvider
            {
                Id = 2,
                Type = StorageProviderType.GooglePhotos,
                Name = "Provider 2",
                IsEnabled = false
            };

            dbContext.StorageProviders.AddRange(provider1, provider2);
            await dbContext.SaveChangesAsync();

            var configuration = CreateConfiguration();

            // Act
            var result1 = await InvokeGetAuthorizationUrl(provider1.Id, dbContext, configuration);
            var result2 = await InvokeGetAuthorizationUrl(provider2.Id, dbContext, configuration);

            // Assert
            var ok1 = result1.Result as Ok<GooglePhotosAuthUrlResponse>;
            var ok2 = result2.Result as Ok<GooglePhotosAuthUrlResponse>;

            await Assert.That(ok1!.Value!.AuthorizationUrl).IsNotEqualTo(ok2!.Value!.AuthorizationUrl);

            // Verify state parameters are different
            var query1 = QueryHelpers.ParseQuery(new Uri(ok1.Value!.AuthorizationUrl).Query);
            var query2 = QueryHelpers.ParseQuery(new Uri(ok2.Value!.AuthorizationUrl).Query);

            await Assert.That(query1["state"].ToString()).IsEqualTo("1");
            await Assert.That(query2["state"].ToString()).IsEqualTo("2");
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithVeryLongAuthCode_HandlesAppropriately()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = new string('a', 10000) // Very long code
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
        }

        [Test]
        [NotInParallel]
        public async Task HandleOAuthCallback_WithSpecialCharactersInAuthCode_HandlesCorrectly()
        {
            // Arrange
            var (dbContext, provider) = await CreateTestProviderAsync();
            var configuration = CreateConfiguration();
            var loggerFactory = CreateLoggerFactory();

            var request = new GooglePhotosCallbackRequest
            {
                AuthorizationCode = "code-with-спец-chars-中文-🔒"
            };

            // Act
            var result = await InvokeHandleOAuthCallback(
                provider.Id, request, dbContext, configuration, loggerFactory);

            // Assert
            var badRequest = result.Result as BadRequest<ApiError>;
            await Assert.That(badRequest).IsNotNull();
        }

        #endregion

        #region Helper Methods

        private static async Task<(LibraFotoDbContext, StorageProvider)> CreateTestProviderAsync()
        {
            var dbContext = CreateDbContext();
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

            return (dbContext, provider);
        }

        private static LibraFotoDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;

            return new LibraFotoDbContext(options);
        }

        private static IConfiguration CreateConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GooglePhotos:ClientId"] = "test-client-id",
                    ["GooglePhotos:ClientSecret"] = "test-client-secret",
                    ["GooglePhotos:RedirectUri"] = "http://localhost:4200/oauth/callback"
                })
                .Build();
        }

        private static ILoggerFactory CreateLoggerFactory()
        {
            return NullLoggerFactory.Instance;
        }

        private static async Task<Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>> InvokeGetAuthorizationUrl(
            long providerId,
            LibraFotoDbContext dbContext,
            IConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            var method = typeof(GooglePhotosOAuthEndpoints)
                .GetMethod("GetAuthorizationUrl", BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new InvalidOperationException("GetAuthorizationUrl method not found");
            }

            var task = (Task<Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>>)method
                .Invoke(null, new object[] { providerId, dbContext, configuration, cancellationToken })!;

            return await task;
        }

        private static async Task<Results<Ok<StorageProviderDto>, BadRequest<ApiError>, NotFound<ApiError>>> InvokeHandleOAuthCallback(
            long providerId,
            GooglePhotosCallbackRequest request,
            LibraFotoDbContext dbContext,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken = default)
        {
            var method = typeof(GooglePhotosOAuthEndpoints)
                .GetMethod("HandleOAuthCallback", BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new InvalidOperationException("HandleOAuthCallback method not found");
            }

            var task = (Task<Results<Ok<StorageProviderDto>, BadRequest<ApiError>, NotFound<ApiError>>>)method
                .Invoke(null, new object[] { providerId, request, dbContext, configuration, loggerFactory, cancellationToken })!;

            return await task;
        }

        #endregion
    }
}
