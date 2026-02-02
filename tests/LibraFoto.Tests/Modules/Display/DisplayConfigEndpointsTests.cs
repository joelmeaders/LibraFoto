using System.Reflection;
using LibraFoto.Modules.Display.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Display;

public class DisplayConfigEndpointsTests
{
    [Test]
    public async Task GetDisplayConfig_UsesMachineLanIp_WhenRelativePathConfigured()
    {
        // Arrange - user accesses via localhost, but we should use machine's LAN IP for QR code
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContext("http", "localhost:3000");
        var machineLanIp = "192.168.1.50";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - should use machine's LAN IP, not localhost
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://192.168.1.50:3000/admin");
    }

    [Test]
    public async Task GetDisplayConfig_ReplacesLocalhostWithMachineLanIp_WhenAbsoluteUrlWithLocalhostConfigured()
    {
        // Arrange - development scenario where config has localhost but user needs QR code to work on phone
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("http://localhost:4200");

        var httpContext = CreateHttpContext("http", "localhost:3000");
        var machineLanIp = "192.168.1.50";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - localhost in admin URL should be replaced with machine's LAN IP
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://192.168.1.50:4200");
    }

    [Test]
    public async Task GetDisplayConfig_FallsBackToRequestHost_WhenNoMachineLanIpAvailable()
    {
        // Arrange - no network interface scenario (rare, but possible)
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContext("http", "localhost:3000");
        string? machineLanIp = null;

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - should fall back to request host when no LAN IP available
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://localhost:3000/admin");
    }

    [Test]
    public async Task GetDisplayConfig_PreservesAbsoluteUrl_WhenNotLocalhost()
    {
        // Arrange - production scenario where config has a real hostname
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("https://admin.example.com");

        var httpContext = CreateHttpContext("http", "192.168.1.100:5179");
        var machineLanIp = "192.168.1.50";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - absolute URL without localhost should be preserved as-is
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("https://admin.example.com");
    }

    [Test]
    public async Task GetDisplayConfig_UsesDefaultAdminPath_WhenNotConfigured()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns((string?)null);

        var httpContext = CreateHttpContext("http", "localhost:8080");
        var machineLanIp = "10.0.0.50";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://10.0.0.50:8080/admin");
    }

    [Test]
    public async Task GetDisplayConfig_HandlesHttpScheme()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContext("http", "myframe.local");
        var machineLanIp = "192.168.1.10";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - uses machine IP, no port when request doesn't have one
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://192.168.1.10/admin");
    }

    [Test]
    public async Task GetDisplayConfig_HandlesHostWithPort()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/settings");

        var httpContext = CreateHttpContext("https", "frame.example.com:443");
        var machineLanIp = "192.168.1.10";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - machine IP with port from request
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("https://192.168.1.10:443/settings");
    }

    [Test]
    public async Task GetDisplayConfig_UsesForwardedHost_WhenPresent()
    {
        // Arrange - simulating nginx proxy scenario (forwarded headers take priority over machine IP)
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContextWithForwardedHeaders(
            scheme: "http",
            host: "localhost:8080",
            forwardedHost: "192.168.1.100",
            forwardedProto: null);
        var machineLanIp = "10.0.0.5"; // Should be ignored when forwarded headers present

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - uses forwarded host, not machine IP
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://192.168.1.100/admin");
    }

    [Test]
    public async Task GetDisplayConfig_UsesForwardedProto_WhenPresent()
    {
        // Arrange - simulating HTTPS termination at proxy
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContextWithForwardedHeaders(
            scheme: "http",
            host: "localhost:8080",
            forwardedHost: null,
            forwardedProto: "https");
        var machineLanIp = "192.168.1.50";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - uses forwarded proto with machine IP
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("https://192.168.1.50:8080/admin");
    }

    [Test]
    public async Task GetDisplayConfig_UsesBothForwardedHeaders_WhenPresent()
    {
        // Arrange - full proxy scenario with both headers (production nginx)
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContextWithForwardedHeaders(
            scheme: "http",
            host: "api:8080",
            forwardedHost: "192.168.1.100:80",
            forwardedProto: "https");
        var machineLanIp = "10.0.0.5"; // Should be ignored when forwarded host present

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - uses both forwarded headers, ignoring machine IP
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("https://192.168.1.100:80/admin");
    }

    [Test]
    public async Task GetDisplayConfig_UsesForwardedHostPort_ForRelativePath()
    {
        // Arrange - proxy scenario where forwarded host has a port
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("/admin");

        var httpContext = CreateHttpContextWithForwardedHeaders(
            scheme: "http",
            host: "internal-api:8080",
            forwardedHost: "public.example.com:8443",
            forwardedProto: "https");
        var machineLanIp = "10.0.0.5";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - uses forwarded host with its port
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("https://public.example.com:8443/admin");
    }

    [Test]
    public async Task GetDisplayConfig_HandlesLocalhostInConfigWithForwardedHeaders()
    {
        // Arrange - localhost in config but behind proxy
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendUrls:AdminUrl"].Returns("http://localhost:4200");

        var httpContext = CreateHttpContextWithForwardedHeaders(
            scheme: "http",
            host: "api:8080",
            forwardedHost: "192.168.1.100",
            forwardedProto: "https");
        var machineLanIp = "10.0.0.5";

        // Act
        var result = DisplayConfigEndpoints.GetDisplayConfig(configuration, httpContext, machineLanIp);

        // Assert - localhost replaced with forwarded host (priority over machine IP)
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value!.AdminUrl).IsEqualTo("http://192.168.1.100:4200");
    }

    [Test]
    public async Task GetMachineLanIp_ReturnsNonLoopbackIpv4()
    {
        // Act
        var result = DisplayConfigEndpoints.GetMachineLanIp();

        // Assert - may return null if no network, but if present should be valid IPv4
        if (result != null)
        {
            await Assert.That(result).IsNotEqualTo("127.0.0.1");
            await Assert.That(result).DoesNotContain(":"); // Not IPv6
            await Assert.That(System.Net.IPAddress.TryParse(result, out _)).IsTrue();
        }
    }

    private static HttpContext CreateHttpContext(string scheme, string host)
    {
        return CreateHttpContextWithForwardedHeaders(scheme, host, null, null);
    }

    private static HttpContext CreateHttpContextWithForwardedHeaders(
        string scheme,
        string host,
        string? forwardedHost,
        string? forwardedProto)
    {
        var httpContext = Substitute.For<HttpContext>();
        var request = Substitute.For<HttpRequest>();
        var headers = new HeaderDictionary();

        if (forwardedHost != null)
        {
            headers["X-Forwarded-Host"] = new StringValues(forwardedHost);
        }
        if (forwardedProto != null)
        {
            headers["X-Forwarded-Proto"] = new StringValues(forwardedProto);
        }

        request.Scheme.Returns(scheme);
        request.Host.Returns(new HostString(host));
        request.Headers.Returns(headers);
        httpContext.Request.Returns(request);

        return httpContext;
    }
}
