using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Storage.Endpoints;

public static class CacheEndpoints
{
    public static IEndpointRouteBuilder MapCacheEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/cache")
            .WithTags("Cache Management");
        // .RequireAuthorization(Policies.Editor);

        group.MapGet("/status", GetCacheStatus)
            .WithName("GetCacheStatus")
            .WithSummary("Get cache statistics");

        group.MapGet("/files", GetCachedFiles)
            .WithName("GetCachedFiles")
            .WithSummary("Get paginated list of cached files");

        group.MapPost("/clear", ClearCache)
            .WithName("ClearCache")
            .WithSummary("Clear all cached files");

        group.MapPost("/evict", TriggerEviction)
            .WithName("TriggerCacheEviction")
            .WithSummary("Manually trigger LRU eviction");

        group.MapDelete("/files/{fileHash}", DeleteCachedFile)
            .WithName("DeleteCachedFile")
            .WithSummary("Delete a specific cached file");

        return app;
    }

    private static async Task<Ok<CacheStatusResponse>> GetCacheStatus(
        [FromServices] ICacheService cacheService,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var size = await cacheService.GetCacheSizeAsync(cancellationToken);
        var count = await cacheService.GetCacheCountAsync(cancellationToken);
        var maxSize = configuration.GetValue<long>("Cache:MaxSizeBytes", 5L * 1024 * 1024 * 1024);

        return TypedResults.Ok(new CacheStatusResponse
        {
            TotalSizeBytes = size,
            FileCount = count,
            MaxSizeBytes = maxSize,
            UsagePercent = maxSize > 0 ? (double)size / maxSize * 100 : 0
        });
    }

    private static async Task<Ok<PagedResult<CachedFileDto>>> GetCachedFiles(
        [FromServices] ICacheService cacheService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var (files, totalCount) = await cacheService.GetCachedFilesAsync(page, pageSize, cancellationToken);

        var dtos = files.Select(f => new CachedFileDto
        {
            FileHash = f.FileHash,
            OriginalUrl = f.OriginalUrl,
            ProviderId = f.ProviderId,
            ProviderName = f.Provider?.Name,
            FileSizeBytes = f.FileSize,
            ContentType = f.ContentType,
            CachedDate = f.CachedDate,
            LastAccessedDate = f.LastAccessedDate,
            AccessCount = f.AccessCount
        }).ToArray();

        var pagination = new PaginationInfo(
            Page: page,
            PageSize: pageSize,
            TotalItems: totalCount,
            TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize)
        );

        return TypedResults.Ok(new PagedResult<CachedFileDto>(dtos, pagination));
    }

    private static async Task<Ok<object>> ClearCache(
        [FromServices] ICacheService cacheService,
        CancellationToken cancellationToken)
    {
        await cacheService.ClearCacheAsync(cancellationToken);

        return TypedResults.Ok<object>(new { Message = "Cache cleared successfully" });
    }

    private static async Task<Ok<object>> TriggerEviction(
        [FromServices] ICacheService cacheService,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var maxSize = configuration.GetValue<long>("Cache:MaxSizeBytes", 5L * 1024 * 1024 * 1024);
        var evicted = await cacheService.EvictLRUAsync(maxSize, cancellationToken);

        return TypedResults.Ok<object>(new { FilesEvicted = evicted });
    }

    private static async Task<Results<Ok, NotFound<ApiError>>> DeleteCachedFile(
        string fileHash,
        [FromServices] ICacheService cacheService,
        CancellationToken cancellationToken)
    {
        await cacheService.DeleteCachedFileAsync(fileHash, cancellationToken);

        return TypedResults.Ok();
    }
}

public record CacheStatusResponse
{
    public long TotalSizeBytes { get; init; }
    public int FileCount { get; init; }
    public long MaxSizeBytes { get; init; }
    public double UsagePercent { get; init; }
}

public record CachedFileDto
{
    public required string FileHash { get; init; }
    public required string OriginalUrl { get; init; }
    public long ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public long FileSizeBytes { get; init; }
    public required string ContentType { get; init; }
    public DateTime CachedDate { get; init; }
    public DateTime LastAccessedDate { get; init; }
    public int AccessCount { get; init; }
}
