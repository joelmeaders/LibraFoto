using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Total count of photos.
/// </summary>
public sealed class GetPhotoCountEndpoint : EndpointWithoutRequest<Ok<PhotoCountDto>>
{
    public override void Configure()
    {
        Get("/api/admin/photos/count");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Get total count of photos";
        });
    }

    public override async Task<Ok<PhotoCountDto>> ExecuteAsync(CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        return await HandleRequestAsync(photoService, ct);
    }

    internal static async Task<Ok<PhotoCountDto>> HandleRequestAsync(IPhotoService photoService, CancellationToken ct)
    {
        var result = await photoService.GetPhotoCountAsync(ct);
        return TypedResults.Ok(result);
    }
}
