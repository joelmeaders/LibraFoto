using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Display.Endpoints
{
    /// <summary>
    /// Endpoints for display configuration.
    /// Provides configuration information for the display frontend.
    /// </summary>
    public static class DisplayConfigEndpoints
    {
        /// <summary>
        /// Maps display config endpoints.
        /// </summary>
        public static IEndpointRouteBuilder MapDisplayConfigEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/display/config")
                .WithTags("DisplayConfig");

            group.MapGet("/", (IConfiguration configuration, HttpContext httpContext) =>
                {
                    // Allow environment variable override for cases where auto-detection doesn't work
                    var envHostIp = Environment.GetEnvironmentVariable("LIBRAFOTO_HOST_IP");
                    var machineLanIp = !string.IsNullOrEmpty(envHostIp) ? envHostIp : GetMachineLanIp();
                    return GetDisplayConfig(configuration, httpContext, machineLanIp);
                })
                .WithName("GetDisplayConfig")
                .WithSummary("Get display configuration")
                .WithDescription("Returns configuration for the display frontend including admin URL for QR code generation.");

            return app;
        }

        /// <summary>
        /// Gets the first non-loopback IPv4 address of the host machine.
        /// Uses NetworkInterface to get a more reliable IP, preferring physical adapters.
        /// Returns null if no LAN IP is found or if running in a Docker container.
        /// </summary>
        /// <remarks>
        /// This is primarily used for development scenarios where QR codes need to work from phones.
        /// In Docker/production, X-Forwarded-Host from the reverse proxy takes priority.
        /// Docker containers will return null to force fallback to the Host header from nginx.
        /// </remarks>
        internal static string? GetMachineLanIp()
        {
            // Known virtual adapter prefixes to filter out
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

                        // Detect Docker internal network IPs (172.16.0.0/12)
                        // These are container IPs, not the host's network IP
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
                // If we can't enumerate network interfaces, return null
                return null;
            }
        }

        /// <summary>
        /// Checks if an IP address is in the Docker internal network range (172.16.0.0/12).
        /// Docker uses this range for bridge networks.
        /// </summary>
        private static bool IsDockerInternalIp(string ipString)
        {
            if (!IPAddress.TryParse(ipString, out var address))
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            // Docker internal range: 172.16.0.0 to 172.31.255.255
            // First byte must be 172, second byte must be 16-31
            return bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
        }

        /// <summary>
        /// Checks if an IP address is a link-local address (169.254.x.x for IPv4).
        /// Link-local addresses are auto-assigned when DHCP is unavailable and won't work across networks.
        /// </summary>
        private static bool IsLinkLocalAddress(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            // Link-local range: 169.254.0.0/16
            return bytes[0] == 169 && bytes[1] == 254;
        }

        /// <summary>
        /// Gets display configuration including admin URL.
        /// This method allows injecting the machine IP for testing.
        /// </summary>
        internal static Ok<DisplayConfigResponse> GetDisplayConfig(
            IConfiguration configuration,
            HttpContext httpContext,
            string? machineLanIp)
        {
            var adminUrl = configuration["FrontendUrls:AdminUrl"] ?? "/admin";
            var request = httpContext.Request;

            // Get the real host from forwarded headers or request (fallback for proxy scenarios)
            // Priority: X-Forwarded-Host > Host header
            var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var requestHost = request.Host.ToString();

            // Use forwarded scheme if available (for HTTPS behind proxy)
            var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                         ?? request.Scheme;

            // Determine the host (with port) to use for the admin URL
            // Priority: X-Forwarded-Host (if not localhost) > Machine LAN IP + port > Request Host
            // We skip X-Forwarded-Host if it contains "localhost" because that won't work for QR codes
            // scanned from external devices (Vite proxy sets X-Forwarded-Host to localhost:3000)
            string hostWithPort;
            string hostIpOnly;
            if (!string.IsNullOrEmpty(forwardedHost) &&
                !forwardedHost.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            {
                // In production behind nginx, use the forwarded host (already contains port if needed)
                hostWithPort = forwardedHost;
                hostIpOnly = GetHostWithoutPort(forwardedHost);
            }
            else if (!string.IsNullOrEmpty(machineLanIp))
            {
                // In development, use the machine's actual LAN IP so QR codes work from phones
                // Add the port from the request since machine IP doesn't include it
                var port = request.Host.Port;
                hostWithPort = port.HasValue ? $"{machineLanIp}:{port}" : machineLanIp;
                hostIpOnly = machineLanIp;
            }
            else
            {
                // Fallback to request host if no LAN IP available (already contains port if needed)
                hostWithPort = requestHost;
                hostIpOnly = GetHostWithoutPort(requestHost);
            }

            // If the admin URL is a relative path, make it absolute
            if (adminUrl.StartsWith('/'))
            {
                adminUrl = $"{scheme}://{hostWithPort}{adminUrl}";
            }
            // If the admin URL contains localhost, replace it with the host IP
            // This handles development scenarios where the config has localhost but
            // the user needs to scan a QR code with their phone
            else if (adminUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Replace localhost with just the IP (without port), preserving the port from the config
                adminUrl = System.Text.RegularExpressions.Regex.Replace(
                    adminUrl,
                    @"localhost",
                    hostIpOnly,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return TypedResults.Ok(new DisplayConfigResponse(adminUrl));
        }

        /// <summary>
        /// Extracts just the host part without the port.
        /// Handles both IPv4 ("192.168.1.100:8080" -> "192.168.1.100")
        /// and IPv6 ("[::1]:8080" -> "[::1]") formats.
        /// </summary>
        private static string GetHostWithoutPort(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return host;
            }

            // Handle IPv6 format: [::1]:8080 or [::1]
            if (host.StartsWith('['))
            {
                var closeBracketIndex = host.IndexOf(']');
                if (closeBracketIndex > 0)
                {
                    // Return the IPv6 address including brackets
                    return host.Substring(0, closeBracketIndex + 1);
                }
                return host;
            }

            // Handle IPv4 format: 192.168.1.100:8080
            if (!host.Contains(':'))
            {
                return host;
            }

            return host.Substring(0, host.LastIndexOf(':'));
        }
    }

    /// <summary>
    /// Response for display config endpoint.
    /// </summary>
    public record DisplayConfigResponse(string AdminUrl);
}
