using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Update a photo's metadata.
/// </summary>
public sealed class UpdatePhotoEndpoint : Endpoint<UpdatePhotoRequest, Results<Ok<PhotoDetailDto>, NotFound>>
{
    public override void Configure()
    {
        Put("/api/admin/photos/{id:long}");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Update a photo's metadata";
        });
    }

    public override async Task<Results<Ok<PhotoDetailDto>, NotFound>> ExecuteAsync(UpdatePhotoRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, photoService, ct);
    }

    internal static async Task<Results<Ok<PhotoDetailDto>, NotFound>> HandleRequestAsync(
        long id,
        UpdatePhotoRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var photo = await photoService.UpdatePhotoAsync(id, request, ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(photo);
    }
}
