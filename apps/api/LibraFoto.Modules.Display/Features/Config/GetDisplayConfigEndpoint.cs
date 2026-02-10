using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Display.Features.Config;

/// <summary>
/// Get display configuration for the frontend.
/// </summary>
public sealed class GetDisplayConfigEndpoint : EndpointWithoutRequest<Ok<DisplayConfigResponse>>
{
    public override void Configure()
    {
        Get("/api/display/config");
        AllowAnonymous();
        Tags("DisplayConfig");
        Summary(s =>
        {
            s.Summary = "Get display configuration";
            s.Description = "Returns configuration for the display frontend including admin URL for QR code generation.";
        });
    }

    public override Task<Ok<DisplayConfigResponse>> ExecuteAsync(CancellationToken ct)
    {
        var configuration = Resolve<IConfiguration>();

        var envHostIp = Environment.GetEnvironmentVariable("LIBRAFOTO_HOST_IP");
        var machineLanIp = !string.IsNullOrEmpty(envHostIp) ? envHostIp : GetMachineLanIp();
        var response = GetDisplayConfig(configuration, HttpContext, machineLanIp);

        return Task.FromResult(response);
    }

    internal static string? GetMachineLanIp()
    {
        string[] virtualPrefixes = ["vEthernet", "docker", "br-", "veth", "virbr"];

        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(ni => !virtualPrefixes.Any(prefix =>
                    ni.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var networkInterface in networkInterfaces)
            {
                var ipProperties = networkInterface.GetIPProperties();
                var unicastAddress = ipProperties.UnicastAddresses
                    .FirstOrDefault(ua =>
                        ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ua.Address) &&
                        !IsLinkLocalAddress(ua.Address));

                if (unicastAddress != null)
                {
                    var ipAddress = unicastAddress.Address.ToString();

                    if (IsDockerInternalIp(ipAddress))
                    {
                        return null;
                    }

                    return ipAddress;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDockerInternalIp(string ipString)
    {
        if (!IPAddress.TryParse(ipString, out var address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
    }

    private static bool IsLinkLocalAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    internal static Ok<DisplayConfigResponse> GetDisplayConfig(
        IConfiguration configuration,
        HttpContext httpContext,
        string? machineLanIp)
    {
        var adminUrl = configuration["FrontendUrls:AdminUrl"] ?? "/admin";
        var request = httpContext.Request;

        var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var requestHost = request.Host.ToString();

        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                     ?? request.Scheme;

        string hostWithPort;
        string hostIpOnly;
        if (!string.IsNullOrEmpty(forwardedHost) &&
            !forwardedHost.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            hostWithPort = forwardedHost;
            hostIpOnly = GetHostWithoutPort(forwardedHost);
        }
        else if (!string.IsNullOrEmpty(machineLanIp))
        {
            var port = request.Host.Port;
            hostWithPort = port.HasValue ? $"{machineLanIp}:{port}" : machineLanIp;
            hostIpOnly = machineLanIp;
        }
        else
        {
            hostWithPort = requestHost;
            hostIpOnly = GetHostWithoutPort(requestHost);
        }

        if (adminUrl.StartsWith('/'))
        {
            adminUrl = $"{scheme}://{hostWithPort}{adminUrl}";
        }
        else if (adminUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            adminUrl = System.Text.RegularExpressions.Regex.Replace(
                adminUrl,
                @"localhost",
                hostIpOnly,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return TypedResults.Ok(new DisplayConfigResponse(adminUrl));
    }

    private static string GetHostWithoutPort(string host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return host;
        }

        if (host.StartsWith('['))
        {
            var closeBracketIndex = host.IndexOf(']');
            if (closeBracketIndex > 0)
            {
                return host.Substring(0, closeBracketIndex + 1);
            }
            return host;
        }

        if (!host.Contains(':'))
        {
            return host;
        }

        return host.Substring(0, host.LastIndexOf(':'));
    }
}

public record DisplayConfigResponse(string AdminUrl);
