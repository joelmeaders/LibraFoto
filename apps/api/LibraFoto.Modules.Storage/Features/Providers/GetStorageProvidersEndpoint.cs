using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Services.Repositories;
using LibraFoto.Modules.Storage.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Providers;

/// <summary>
/// List all storage providers.
/// </summary>
public sealed class GetStorageProvidersEndpoint : EndpointWithoutRequest<Ok<StorageProviderDto[]>>
{
    private static readonly string[] _googlePhotosRequiredScopes =
    [
        "https://www.googleapis.com/auth/photospicker.mediaitems.readonly"
    ];

    public override void Configure()
    {
        Get("/api/admin/storage/providers");
        Tags("Storage Providers");
        Summary(s =>
        {
            s.Summary = "Get all storage providers";
            s.Description = "Returns a list of all configured storage providers.";
        });
    }

    public override async Task<Ok<StorageProviderDto[]>> ExecuteAsync(CancellationToken ct)
    {
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var factory = Resolve<IStorageProviderFactory>();
        return await HandleRequestAsync(storageRepository, factory, ct);
    }

    internal static async Task<Ok<StorageProviderDto[]>> HandleRequestAsync(
        IStoragePersistenceRepository storageRepository,
        IStorageProviderFactory factory,
        CancellationToken cancellationToken)
    {
        var entities = await storageRepository.GetAllProvidersAsync(cancellationToken);

        var providers = new List<StorageProviderDto>();

        foreach (var entity in entities)
        {
            bool? isConnected = null;

            if (entity.Type != StorageProviderType.Local)
            {
                try
                {
                    var provider = await factory.GetProviderAsync(entity.Id, cancellationToken);
                    if (provider != null)
                    {
                        isConnected = await provider.TestConnectionAsync(cancellationToken);
                    }
                }
                catch
                {
                    isConnected = false;
                }
            }

            var photoCount = await storageRepository.CountPhotosByProviderAsync(entity.Id, cancellationToken);

            providers.Add(new StorageProviderDto
            {
                Id = entity.Id,
                Type = entity.Type,
                Name = entity.Name,
                IsEnabled = entity.IsEnabled,
                SupportsUpload = entity.Type == StorageProviderType.Local,
                SupportsWatch = entity.Type == StorageProviderType.Local,
                LastSyncDate = entity.LastSyncDate,
                PhotoCount = photoCount,
                IsConnected = isConnected,
                StatusMessage = GetProviderStatusMessage(entity)
            });
        }

        return TypedResults.Ok(providers.ToArray());
    }

    private static string? GetProviderStatusMessage(Data.Entities.StorageProvider entity)
    {
        if (entity.Type != StorageProviderType.GooglePhotos || string.IsNullOrWhiteSpace(entity.Configuration))
        {
            return null;
        }

        try
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<GooglePhotosConfiguration>(entity.Configuration);
            if (config?.GrantedScopes is { Length: > 0 } && !HasRequiredScopes(config.GrantedScopes))
            {
                var missingScopes = _googlePhotosRequiredScopes
                    .Where(scope => !config.GrantedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
                    .Select(FormatScope)
                    .ToArray();

                return missingScopes.Length == 0
                    ? "Reconnect required: missing Google Photos permissions."
                    : $"Reconnect required: missing Google Photos permissions ({string.Join(", ", missingScopes)}).";
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool HasRequiredScopes(IEnumerable<string> grantedScopes)
    {
        var scopeSet = new HashSet<string>(grantedScopes, StringComparer.OrdinalIgnoreCase);
        return _googlePhotosRequiredScopes.All(scopeSet.Contains);
    }

    private static string FormatScope(string scope)
    {
        var lastSlash = scope.LastIndexOf('/');
        return lastSlash >= 0 ? scope[(lastSlash + 1)..] : scope;
    }
}
