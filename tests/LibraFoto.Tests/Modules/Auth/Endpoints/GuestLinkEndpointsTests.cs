using System.Security.Claims;
using LibraFoto.Modules.Auth.Features.GuestLinks.Admin;
using LibraFoto.Modules.Auth.Features.GuestLinks.Public;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Auth.Endpoints;

/// <summary>
/// Tests for GuestLinkEndpoints - guest link management endpoints.
/// </summary>
public class GuestLinkEndpointsTests
{
    private IGuestLinkService _guestLinkService = null!;

    [Before(Test)]
    public void Setup()
    {
        _guestLinkService = Substitute.For<IGuestLinkService>();
    }

    #region GetGuestLinks Tests

    [Test]
    public async Task GetGuestLinks_WithDefaultParams_ReturnsPagedResult()
    {
        // Arrange
        var links = new[]
        {
            CreateGuestLinkDto("1", "Link 1"),
            CreateGuestLinkDto("2", "Link 2")
        };
        _guestLinkService.GetGuestLinksAsync(1, 20, false, Arg.Any<CancellationToken>())
            .Returns((links, 2));

        // Act
        var result = await GetGuestLinksEndpoint.HandleRequestAsync(
            new GetGuestLinksRequest { Page = 0, PageSize = 0, IncludeExpired = false },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Data.Count()).IsEqualTo(2);
        await Assert.That(result.Value.Pagination.Page).IsEqualTo(1);
        await Assert.That(result.Value.Pagination.PageSize).IsEqualTo(20);
    }

    [Test]
    public async Task GetGuestLinks_WithCustomPageSize_ReturnsCorrectPage()
    {
        // Arrange
        var links = new[] { CreateGuestLinkDto("1", "Link 1") };
        _guestLinkService.GetGuestLinksAsync(2, 10, false, Arg.Any<CancellationToken>())
            .Returns((links, 25));

        // Act
        var result = await GetGuestLinksEndpoint.HandleRequestAsync(
            new GetGuestLinksRequest { Page = 2, PageSize = 10, IncludeExpired = false },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Pagination.Page).IsEqualTo(2);
        await Assert.That(result.Value.Pagination.PageSize).IsEqualTo(10);
        await Assert.That(result.Value.Pagination.TotalItems).IsEqualTo(25);
        await Assert.That(result.Value.Pagination.TotalPages).IsEqualTo(3);
    }

    [Test]
    public async Task GetGuestLinks_WithExcessivePageSize_ClampsTo100()
    {
        // Arrange
        var links = Array.Empty<GuestLinkDto>();
        _guestLinkService.GetGuestLinksAsync(1, 20, false, Arg.Any<CancellationToken>())
            .Returns((links, 0));

        // Act
        await GetGuestLinksEndpoint.HandleRequestAsync(
            new GetGuestLinksRequest { Page = 1, PageSize = 500, IncludeExpired = false },
            _guestLinkService,
            CancellationToken.None);

        // Assert - should clamp to default 20, not 100
        await _guestLinkService.Received(1).GetGuestLinksAsync(
            1, 20, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetGuestLinks_WithIncludeExpired_PassesToService()
    {
        // Arrange
        var links = Array.Empty<GuestLinkDto>();
        _guestLinkService.GetGuestLinksAsync(1, 20, true, Arg.Any<CancellationToken>())
            .Returns((links, 0));

        // Act
        await GetGuestLinksEndpoint.HandleRequestAsync(
            new GetGuestLinksRequest { Page = 1, PageSize = 20, IncludeExpired = true },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await _guestLinkService.Received(1).GetGuestLinksAsync(
            1, 20, true, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetGuestLinkById Tests

    [Test]
    public async Task GetGuestLinkById_WithValidId_ReturnsOkWithLink()
    {
        // Arrange
        var link = CreateGuestLinkDto("123", "Test Link");
        _guestLinkService.GetGuestLinkByIdAsync("123", Arg.Any<CancellationToken>())
            .Returns(link);

        // Act
        var result = await GetGuestLinkByIdEndpoint.HandleRequestAsync(
            new GetGuestLinkByIdRequest { Id = "123" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<GuestLinkDto>>();
        var okResult = (Ok<GuestLinkDto>)result.Result;
        await Assert.That(okResult.Value).IsNotNull();
        await Assert.That(okResult.Value!.Id).IsEqualTo("123");
    }

    [Test]
    public async Task GetGuestLinkById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _guestLinkService.GetGuestLinkByIdAsync("999", Arg.Any<CancellationToken>())
            .Returns((GuestLinkDto?)null);

        // Act
        var result = await GetGuestLinkByIdEndpoint.HandleRequestAsync(
            new GetGuestLinkByIdRequest { Id = "999" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    #endregion

    #region CreateGuestLink Tests

    [Test]
    public async Task CreateGuestLink_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var user = CreateClaimsPrincipal(100);
        var request = new CreateGuestLinkRequest("New Link", null, null, null);
        var createdLink = CreateGuestLinkDto("new-id", "New Link");
        _guestLinkService.CreateGuestLinkAsync(request, 100, Arg.Any<CancellationToken>())
            .Returns(createdLink);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Created<GuestLinkDto>>();
        var createdResult = (Created<GuestLinkDto>)result.Result;
        await Assert.That(createdResult.Location).IsEqualTo("/api/admin/guest-links/new-id");
        await Assert.That(createdResult.Value).IsNotNull();
    }

    [Test]
    public async Task CreateGuestLink_WithEmptyName_ReturnsValidationProblem()
    {
        // Arrange
        var user = CreateClaimsPrincipal(100);
        var request = new CreateGuestLinkRequest("", null, null, null);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
        var validationResult = (ValidationProblem)result.Result;
        await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("name");
    }

    [Test]
    public async Task CreateGuestLink_WithWhitespaceName_ReturnsValidationProblem()
    {
        // Arrange
        var user = CreateClaimsPrincipal(100);
        var request = new CreateGuestLinkRequest("   ", null, null, null);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
    }

    [Test]
    public async Task CreateGuestLink_WithPastExpirationDate_ReturnsValidationProblem()
    {
        // Arrange
        var user = CreateClaimsPrincipal(100);
        var request = new CreateGuestLinkRequest(
            "Test Link",
            DateTime.UtcNow.AddDays(-1),
            null,
            null);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
        var validationResult = (ValidationProblem)result.Result;
        await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("expiresAt");
    }

    [Test]
    public async Task CreateGuestLink_WithZeroMaxUploads_ReturnsValidationProblem()
    {
        // Arrange
        var user = CreateClaimsPrincipal(100);
        var request = new CreateGuestLinkRequest("Test Link", null, 0, null);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
        var validationResult = (ValidationProblem)result.Result;
        await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("maxUploads");
    }

    [Test]
    public async Task CreateGuestLink_WithNegativeMaxUploads_ReturnsValidationProblem()
    {
        // Arrange
        var user = CreateClaimsPrincipal(100);
        var request = new CreateGuestLinkRequest("Test Link", null, -5, null);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
    }

    [Test]
    public async Task CreateGuestLink_WithNoUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var request = new CreateGuestLinkRequest("Test Link", null, null, null);

        // Act
        var result = await CreateGuestLinkEndpoint.HandleRequestAsync(
            request, user, _guestLinkService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
    }

    #endregion

    #region DeleteGuestLink Tests

    [Test]
    public async Task DeleteGuestLink_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _guestLinkService.DeleteGuestLinkAsync("123", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await DeleteGuestLinkEndpoint.HandleRequestAsync(
            new DeleteGuestLinkRequest { Id = "123" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NoContent>();
    }

    [Test]
    public async Task DeleteGuestLink_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _guestLinkService.DeleteGuestLinkAsync("999", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await DeleteGuestLinkEndpoint.HandleRequestAsync(
            new DeleteGuestLinkRequest { Id = "999" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    #endregion

    #region GetMyGuestLinks Tests

    [Test]
    public async Task GetMyGuestLinks_WithValidUser_ReturnsOkWithLinks()
    {
        // Arrange
        var user = CreateClaimsPrincipal(200);
        var links = new[]
        {
            CreateGuestLinkDto("1", "My Link 1"),
            CreateGuestLinkDto("2", "My Link 2")
        };
        _guestLinkService.GetGuestLinksByUserAsync(200, Arg.Any<CancellationToken>())
            .Returns(links);

        // Act
        var result = await GetMyGuestLinksEndpoint.HandleRequestAsync(
            user,
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<GuestLinkDto[]>>();
        var okResult = (Ok<GuestLinkDto[]>)result.Result;
        await Assert.That(okResult.Value!.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task GetMyGuestLinks_WithNoUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await GetMyGuestLinksEndpoint.HandleRequestAsync(
            user,
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
    }

    #endregion

    #region ValidateGuestLink Tests

    [Test]
    public async Task ValidateGuestLink_WithValidCode_ReturnsValidationResponse()
    {
        // Arrange
        var validation = new GuestLinkValidationResponse(
            true, "Test Link", "Album Name", 5, null);
        _guestLinkService.ValidateGuestLinkAsync("valid-code", Arg.Any<CancellationToken>())
            .Returns(validation);

        // Act
        var result = await ValidateGuestLinkEndpoint.HandleRequestAsync(
            new ValidateGuestLinkRequest { LinkCode = "valid-code" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.IsValid).IsTrue();
        await Assert.That(result.Value.RemainingUploads).IsEqualTo(5);
    }

    [Test]
    public async Task ValidateGuestLink_WithInvalidCode_ReturnsInvalidResponse()
    {
        // Arrange
        var validation = new GuestLinkValidationResponse(
            false, null, null, null, "Link not found");
        _guestLinkService.ValidateGuestLinkAsync("invalid-code", Arg.Any<CancellationToken>())
            .Returns(validation);

        // Act
        var result = await ValidateGuestLinkEndpoint.HandleRequestAsync(
            new ValidateGuestLinkRequest { LinkCode = "invalid-code" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.IsValid).IsFalse();
    }

    #endregion

    #region GetGuestLinkInfo Tests

    [Test]
    public async Task GetGuestLinkInfo_WithValidCode_ReturnsOkWithInfo()
    {
        // Arrange
        var validation = new GuestLinkValidationResponse(
            true, "Public Link", "Album", 3, null);
        _guestLinkService.ValidateGuestLinkAsync("code123", Arg.Any<CancellationToken>())
            .Returns(validation);

        // Act
        var result = await GetGuestLinkInfoEndpoint.HandleRequestAsync(
            new GetGuestLinkInfoRequest { LinkCode = "code123" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<GuestLinkPublicInfo>>();
        var okResult = (Ok<GuestLinkPublicInfo>)result.Result;
        await Assert.That(okResult.Value).IsNotNull();
        await Assert.That(okResult.Value!.Name).IsEqualTo("Public Link");
        await Assert.That(okResult.Value.IsActive).IsTrue();
    }

    [Test]
    public async Task GetGuestLinkInfo_WithInvalidCode_ReturnsNotFound()
    {
        // Arrange
        var validation = new GuestLinkValidationResponse(
            false, null, null, null, "Not found");
        _guestLinkService.ValidateGuestLinkAsync("bad-code", Arg.Any<CancellationToken>())
            .Returns(validation);

        // Act
        var result = await GetGuestLinkInfoEndpoint.HandleRequestAsync(
            new GetGuestLinkInfoRequest { LinkCode = "bad-code" },
            _guestLinkService,
            CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    #endregion

    #region Helper Methods

    private static ClaimsPrincipal CreateClaimsPrincipal(long userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static GuestLinkDto CreateGuestLinkDto(string id, string name)
    {
        return new GuestLinkDto(
            id,
            name,
            DateTime.UtcNow,
            null,
            null,
            0,
            null,
            null,
            1,
            "testuser",
            true
        );
    }

    #endregion
}
