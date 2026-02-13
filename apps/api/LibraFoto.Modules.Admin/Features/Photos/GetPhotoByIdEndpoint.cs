using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Get detailed information about a photo.
/// </summary>
public sealed class GetPhotoByIdEndpoint : Endpoint<GetPhotoByIdRequest, Results<Ok<PhotoDetailDto>, NotFound>>
{
    public override void Configure()
    {
        Get("/api/admin/photos/{id:long}");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Get detailed information about a photo";
        });
    }

    public override async Task<Results<Ok<PhotoDetailDto>, NotFound>> ExecuteAsync(GetPhotoByIdRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        return await HandleRequestAsync(req.Id, photoService, ct);
    }

    internal static async Task<Results<Ok<PhotoDetailDto>, NotFound>> HandleRequestAsync(
        long id,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var photo = await photoService.GetPhotoByIdAsync(id, ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(photo);
    }
}

public sealed class GetPhotoByIdRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
