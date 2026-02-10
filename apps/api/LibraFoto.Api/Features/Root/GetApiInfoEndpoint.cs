using System.Reflection;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Api.Features.Root;

public record ApiInfo(string Name, string Version);

/// <summary>
/// API info endpoint.
/// </summary>
public sealed class GetApiInfoEndpoint : EndpointWithoutRequest<Ok<ApiInfo>>
{
    public override void Configure()
    {
        Get("/");
    }

    public override Task<Ok<ApiInfo>> ExecuteAsync(CancellationToken ct)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        return Task.FromResult(TypedResults.Ok(new ApiInfo("LibraFoto API", version)));
    }
}
