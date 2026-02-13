using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Storage.Features.Providers;

/// <summary>
/// Get a storage provider by ID.
/// </summary>
public sealed class GetStorageProviderEndpoint : Endpoint<GetStorageProviderRequest, Results<Ok<StorageProviderDto>, NotFound<ApiError>>>
{
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private static readonly string[] _googlePhotosRequiredScopes =
    [
        "https://www.googleapis.com/auth/photospicker.mediaitems.readonly"
    ];

    public override void Configure()
    {
        Get("/api/admin/storage/providers/{id:long}");
        Tags("Storage Providers");
        Summary(s =>
        {
            s.Summary = "Get a storage provider by ID";
            s.Description = "Returns details of a specific storage provider.";
        });
    }

    public override async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>>> ExecuteAsync(
        GetStorageProviderRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var factory = Resolve<IStorageProviderFactory>();
        return await HandleRequestAsync(req.Id, dbContext, factory, ct);
    }

    internal static async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>>> HandleRequestAsync(
        long id,
        LibraFotoDbContext dbContext,
        IStorageProviderFactory factory,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.StorageProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {id} not found"));
        }

        bool? isConnected = null;
        if (entity.Type != StorageProviderType.Local)
        {
            try
            {
                var provider = await factory.GetProviderAsync(id, cancellationToken);
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

        var photoCount = await dbContext.Photos.CountAsync(p => p.ProviderId == id, cancellationToken);

        var dto = new StorageProviderDto
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
        };

        return TypedResults.Ok(dto);
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

public sealed class GetStorageProviderRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
