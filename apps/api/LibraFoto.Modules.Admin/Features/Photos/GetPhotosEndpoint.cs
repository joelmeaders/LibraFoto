using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Paginated list of photos with optional filtering.
/// </summary>
public sealed class GetPhotosEndpoint : Endpoint<GetPhotosRequest, Ok<PagedResult<PhotoListDto>>>
{
    public override void Configure()
    {
        Get("/api/admin/photos");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Get paginated list of photos with optional filtering";
        });
    }

    public override async Task<Ok<PagedResult<PhotoListDto>>> ExecuteAsync(GetPhotosRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        return await HandleRequestAsync(req, photoService, ct);
    }

    internal static async Task<Ok<PagedResult<PhotoListDto>>> HandleRequestAsync(
        GetPhotosRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 50;
        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "DateAdded" : request.SortBy;
        var sortDirection = string.IsNullOrWhiteSpace(request.SortDirection) ? "desc" : request.SortDirection;

        var filter = new PhotoFilterRequest
        {
            Page = page,
            PageSize = pageSize,
            AlbumId = request.AlbumId,
            TagId = request.TagId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            MediaType = request.MediaType,
            Search = request.Search,
            SortBy = sortBy,
            SortDirection = sortDirection
        };

        var result = await photoService.GetPhotosAsync(filter, ct);
        return TypedResults.Ok(result);
    }
}

public sealed class GetPhotosRequest
{
    [QueryParam]
    public int? Page { get; init; }

    [QueryParam]
    public int? PageSize { get; init; }

    [QueryParam]
    public long? AlbumId { get; init; }

    [QueryParam]
    public long? TagId { get; init; }

    [QueryParam]
    public DateTime? DateFrom { get; init; }

    [QueryParam]
    public DateTime? DateTo { get; init; }

    [QueryParam]
    public MediaType? MediaType { get; init; }

    [QueryParam]
    public string? Search { get; init; }

    [QueryParam]
    public string? SortBy { get; init; }

    [QueryParam]
    public string? SortDirection { get; init; }
}
