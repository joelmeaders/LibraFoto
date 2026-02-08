using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Endpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Display
{
    /// <summary>
    /// Comprehensive tests for DisplaySettingsEndpoints covering all endpoint methods.
    /// Tests display configuration management including validation, CRUD operations,
    /// and activation logic.
    /// </summary>
    public class DisplaySettingsEndpointsTests
    {
        private IDisplaySettingsService _settingsService = null!;
        private ISlideshowService _slideshowService = null!;

        [Before(Test)]
        public void Setup()
        {
            _settingsService = Substitute.For<IDisplaySettingsService>();
            _slideshowService = Substitute.For<ISlideshowService>();
        }

        #region GetActiveSettings Tests

        [Test]
        public async Task GetActiveSettings_ReturnsActiveSettings()
        {
            // Arrange
            var expectedSettings = CreateDisplaySettingsDto(1, "Default", 10);
            _settingsService.GetActiveSettingsAsync(Arg.Any<CancellationToken>())
                .Returns(expectedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.GetActiveSettings(_settingsService);

            // Assert
            await Assert.That(result.Value).IsEqualTo(expectedSettings);
            await Assert.That(result.Value!.Name).IsEqualTo("Default");
            await Assert.That(result.Value.SlideDuration).IsEqualTo(10);
        }

        [Test]
        public async Task GetActiveSettings_WithKenBurnsTransition_ReturnsCorrectSettings()
        {
            // Arrange
            var expectedSettings = CreateDisplaySettingsDto(
                id: 1,
                name: "KenBurns",
                slideDuration: 15,
                transition: TransitionType.KenBurns);
            _settingsService.GetActiveSettingsAsync(Arg.Any<CancellationToken>())
                .Returns(expectedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.GetActiveSettings(_settingsService);

            // Assert
            await Assert.That(result.Value!.Transition).IsEqualTo(TransitionType.KenBurns);
        }

        #endregion

        #region GetAllSettings Tests

        [Test]
        public async Task GetAllSettings_WithNoSettings_ReturnsEmptyList()
        {
            // Arrange
            _settingsService.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(Array.Empty<DisplaySettingsDto>());

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.GetAllSettings(_settingsService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(0);
        }

        [Test]
        public async Task GetAllSettings_WithMultipleSettings_ReturnsAll()
        {
            // Arrange
            var settings = new[]
            {
                CreateDisplaySettingsDto(1, "Default", 10),
                CreateDisplaySettingsDto(2, "Fast", 5),
                CreateDisplaySettingsDto(3, "Slow", 20)
            };
            _settingsService.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(settings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.GetAllSettings(_settingsService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(3);
            await Assert.That(result.Value[0].Name).IsEqualTo("Default");
            await Assert.That(result.Value[1].Name).IsEqualTo("Fast");
            await Assert.That(result.Value[2].Name).IsEqualTo("Slow");
        }

        #endregion

        #region GetSettingsById Tests

        [Test]
        public async Task GetSettingsById_WithValidId_ReturnsOkWithSettings()
        {
            // Arrange
            var settings = CreateDisplaySettingsDto(1, "Test Settings", 12);
            _settingsService.GetByIdAsync(1, Arg.Any<CancellationToken>())
                .Returns(settings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.GetSettingsById(1, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
            var okResult = (Ok<DisplaySettingsDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(1);
            await Assert.That(okResult.Value.Name).IsEqualTo("Test Settings");
        }

        [Test]
        public async Task GetSettingsById_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            _settingsService.GetByIdAsync(999, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.GetSettingsById(999, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value).IsNotNull();
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("SETTINGS_NOT_FOUND");
            await Assert.That(notFoundResult.Value.Message).Contains("999");
        }

        #endregion

        #region UpdateSettings Tests

        [Test]
        public async Task UpdateSettings_WithValidRequest_UpdatesAndResetsSlideshowSequence()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "Updated Settings",
                SlideDuration = 15,
                TransitionDuration = 800
            };
            var updatedSettings = CreateDisplaySettingsDto(1, "Updated Settings", 15);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
            var okResult = (Ok<DisplaySettingsDto>)result.Result;
            await Assert.That(okResult.Value!.Name).IsEqualTo("Updated Settings");
            _slideshowService.Received(1).ResetSequence(1);
        }

        [Test]
        public async Task UpdateSettings_WithMinimumSlideDuration_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = 1 };
            var updatedSettings = CreateDisplaySettingsDto(1, "Test", 1);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithZeroSlideDuration_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = 0 };

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequestResult = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequestResult.Value!.Code).IsEqualTo("VALIDATION_ERROR");
            await Assert.That(badRequestResult.Value.Message).Contains("Slide duration must be at least 1 second");
        }

        [Test]
        public async Task UpdateSettings_WithNegativeSlideDuration_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = -5 };

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequestResult = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequestResult.Value!.Message).Contains("at least 1 second");
        }

        [Test]
        public async Task UpdateSettings_WithZeroTransitionDuration_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { TransitionDuration = 0 };
            var updatedSettings = CreateDisplaySettingsDto(1, "Test", 10, transitionDuration: 0);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
            var okResult = (Ok<DisplaySettingsDto>)result.Result;
            await Assert.That(okResult.Value!.TransitionDuration).IsEqualTo(0);
        }

        [Test]
        public async Task UpdateSettings_WithNegativeTransitionDuration_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { TransitionDuration = -100 };

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequestResult = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequestResult.Value!.Code).IsEqualTo("VALIDATION_ERROR");
            await Assert.That(badRequestResult.Value.Message).Contains("Transition duration cannot be negative");
        }

        [Test]
        public async Task UpdateSettings_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { Name = "Test" };
            _settingsService.UpdateAsync(999, request, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                999, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("SETTINGS_NOT_FOUND");
        }

        [Test]
        public async Task UpdateSettings_WithAllTransitionTypes_ResetsSequence()
        {
            // Test Fade
            var request1 = new UpdateDisplaySettingsRequest { Transition = TransitionType.Fade };
            _settingsService.UpdateAsync(1, request1, Arg.Any<CancellationToken>())
                .Returns(CreateDisplaySettingsDto(1, "Test", 10, transition: TransitionType.Fade));
            await DisplaySettingsEndpoints_TestHelper.UpdateSettings(1, request1, _settingsService, _slideshowService);
            _slideshowService.Received(1).ResetSequence(1);

            // Test Slide
            var request2 = new UpdateDisplaySettingsRequest { Transition = TransitionType.Slide };
            _settingsService.UpdateAsync(2, request2, Arg.Any<CancellationToken>())
                .Returns(CreateDisplaySettingsDto(2, "Test", 10, transition: TransitionType.Slide));
            await DisplaySettingsEndpoints_TestHelper.UpdateSettings(2, request2, _settingsService, _slideshowService);
            _slideshowService.Received(1).ResetSequence(2);

            // Test KenBurns
            var request3 = new UpdateDisplaySettingsRequest { Transition = TransitionType.KenBurns };
            _settingsService.UpdateAsync(3, request3, Arg.Any<CancellationToken>())
                .Returns(CreateDisplaySettingsDto(3, "Test", 10, transition: TransitionType.KenBurns));
            await DisplaySettingsEndpoints_TestHelper.UpdateSettings(3, request3, _settingsService, _slideshowService);
            _slideshowService.Received(1).ResetSequence(3);
        }

        [Test]
        public async Task UpdateSettings_WithSourceTypeAlbum_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                SourceType = SourceType.Album,
                SourceId = 5
            };
            var updatedSettings = CreateDisplaySettingsDto(1, "Album Settings", 10);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithSourceTypeTag_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                SourceType = SourceType.Tag,
                SourceId = 10
            };
            var updatedSettings = CreateDisplaySettingsDto(1, "Tag Settings", 10);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithImageFitCover_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { ImageFit = ImageFit.Cover };
            var updatedSettings = CreateDisplaySettingsDto(1, "Test", 10);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithShuffleToggle_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { Shuffle = false };
            var updatedSettings = CreateDisplaySettingsDto(1, "Test", 10);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithAllFieldsUpdated_UpdatesSuccessfully()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "Complete Update",
                SlideDuration = 30,
                Transition = TransitionType.KenBurns,
                TransitionDuration = 2000,
                SourceType = SourceType.Album,
                SourceId = 15,
                Shuffle = false,
                ImageFit = ImageFit.Cover
            };
            var updatedSettings = CreateDisplaySettingsDto(1, "Complete Update", 30);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
            _slideshowService.Received(1).ResetSequence(1);
        }

        #endregion

        #region CreateSettings Tests

        [Test]
        public async Task CreateSettings_WithValidRequest_CreatesSettings()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "New Settings",
                SlideDuration = 8
            };
            var createdSettings = CreateDisplaySettingsDto(2, "New Settings", 8);
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<DisplaySettingsDto>>();
            var createdResult = (Created<DisplaySettingsDto>)result.Result;
            await Assert.That(createdResult.Location).IsEqualTo("/api/display/settings/2");
            await Assert.That(createdResult.Value).IsNotNull();
            await Assert.That(createdResult.Value!.Name).IsEqualTo("New Settings");
        }

        [Test]
        public async Task CreateSettings_WithMinimumSlideDuration_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = 1 };
            var createdSettings = CreateDisplaySettingsDto(1, "Test", 1);
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<DisplaySettingsDto>>();
        }

        [Test]
        public async Task CreateSettings_WithZeroSlideDuration_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = 0 };

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequestResult = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequestResult.Value!.Code).IsEqualTo("VALIDATION_ERROR");
            await Assert.That(badRequestResult.Value.Message).Contains("Slide duration must be at least 1 second");
        }

        [Test]
        public async Task CreateSettings_WithNegativeSlideDuration_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = -10 };

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        [Test]
        public async Task CreateSettings_WithZeroTransitionDuration_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { TransitionDuration = 0 };
            var createdSettings = CreateDisplaySettingsDto(1, "Test", 10, transitionDuration: 0);
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<DisplaySettingsDto>>();
        }

        [Test]
        public async Task CreateSettings_WithNegativeTransitionDuration_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { TransitionDuration = -500 };

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequestResult = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequestResult.Value!.Code).IsEqualTo("VALIDATION_ERROR");
            await Assert.That(badRequestResult.Value.Message).Contains("Transition duration cannot be negative");
        }

        [Test]
        public async Task CreateSettings_WithCompleteConfiguration_CreatesSuccessfully()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "Custom Config",
                SlideDuration = 25,
                Transition = TransitionType.Slide,
                TransitionDuration = 1500,
                SourceType = SourceType.Tag,
                SourceId = 7,
                Shuffle = true,
                ImageFit = ImageFit.Contain
            };
            var createdSettings = CreateDisplaySettingsDto(5, "Custom Config", 25);
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<DisplaySettingsDto>>();
            var createdResult = (Created<DisplaySettingsDto>)result.Result;
            await Assert.That(createdResult.Location).IsEqualTo("/api/display/settings/5");
        }

        #endregion

        #region DeleteSettings Tests

        [Test]
        public async Task DeleteSettings_WithValidId_DeletesSuccessfully()
        {
            // Arrange
            _settingsService.DeleteAsync(1, Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.DeleteSettings(1, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NoContent>();
        }

        [Test]
        public async Task DeleteSettings_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            _settingsService.DeleteAsync(999, Arg.Any<CancellationToken>())
                .Returns(false);
            _settingsService.GetByIdAsync(999, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.DeleteSettings(999, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("SETTINGS_NOT_FOUND");
            await Assert.That(notFoundResult.Value.Message).Contains("999");
        }

        [Test]
        public async Task DeleteSettings_WhenLastSettings_ReturnsBadRequest()
        {
            // Arrange - delete fails but settings exist
            var existingSettings = CreateDisplaySettingsDto(1, "Last Settings", 10);
            _settingsService.DeleteAsync(1, Arg.Any<CancellationToken>())
                .Returns(false);
            _settingsService.GetByIdAsync(1, Arg.Any<CancellationToken>())
                .Returns(existingSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.DeleteSettings(1, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequestResult = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequestResult.Value!.Code).IsEqualTo("CANNOT_DELETE_LAST");
            await Assert.That(badRequestResult.Value.Message).Contains("Cannot delete the last display settings");
        }

        #endregion

        #region ActivateSettings Tests

        [Test]
        public async Task ActivateSettings_WithValidId_ActivatesAndResetsSlideshowSequence()
        {
            // Arrange
            var activatedSettings = CreateDisplaySettingsDto(2, "Activated", 15);
            _settingsService.SetActiveAsync(2, Arg.Any<CancellationToken>())
                .Returns(activatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.ActivateSettings(
                2, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
            var okResult = (Ok<DisplaySettingsDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(2);
            _slideshowService.Received(1).ResetSequence(null);
        }

        [Test]
        public async Task ActivateSettings_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            _settingsService.SetActiveAsync(999, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.ActivateSettings(
                999, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("SETTINGS_NOT_FOUND");
            await Assert.That(notFoundResult.Value.Message).Contains("999");
        }

        [Test]
        public async Task ActivateSettings_ResetsSequenceWithNullParameter()
        {
            // Arrange
            var activatedSettings = CreateDisplaySettingsDto(3, "New Active", 20);
            _settingsService.SetActiveAsync(3, Arg.Any<CancellationToken>())
                .Returns(activatedSettings);

            // Act
            await DisplaySettingsEndpoints_TestHelper.ActivateSettings(
                3, _settingsService, _slideshowService);

            // Assert - verify ResetSequence called with null to use new active settings
            _slideshowService.Received(1).ResetSequence(null);
        }

        #endregion

        #region Edge Cases Tests

        [Test]
        public async Task UpdateSettings_WithBoundaryValues_HandlesCorrectly()
        {
            // Arrange - test boundary: exactly 1 second for slide duration
            var request = new UpdateDisplaySettingsRequest
            {
                SlideDuration = 1,
                TransitionDuration = 0
            };
            var updatedSettings = CreateDisplaySettingsDto(1, "Boundary", 1, transitionDuration: 0);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task CreateSettings_WithNullOptionalFields_CreatesSuccessfully()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = null,
                SlideDuration = null,
                Transition = null,
                TransitionDuration = null,
                SourceType = null,
                SourceId = null,
                Shuffle = null,
                ImageFit = null
            };
            var createdSettings = CreateDisplaySettingsDto(1, "Default", 10);
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithVeryLargeSlideDuration_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { SlideDuration = 3600 }; // 1 hour
            var updatedSettings = CreateDisplaySettingsDto(1, "Long", 3600);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_WithVeryLargeTransitionDuration_IsValid()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { TransitionDuration = 10000 }; // 10 seconds
            var updatedSettings = CreateDisplaySettingsDto(1, "Slow", 10, transitionDuration: 10000);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
        }

        [Test]
        public async Task UpdateSettings_OnlyNameChanged_StillResetsSlideshowSequence()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { Name = "Renamed" };
            var updatedSettings = CreateDisplaySettingsDto(1, "Renamed", 10);
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.UpdateSettings(
                1, request, _settingsService, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<DisplaySettingsDto>>();
            _slideshowService.Received(1).ResetSequence(1);
        }

        [Test]
        public async Task CreateSettings_WithSourceTypeAllAndSourceId_IsValid()
        {
            // Arrange - SourceId should be ignored when SourceType is All
            var request = new UpdateDisplaySettingsRequest
            {
                SourceType = SourceType.All,
                SourceId = 999 // This should be ignored
            };
            var createdSettings = CreateDisplaySettingsDto(1, "All Photos", 10);
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await DisplaySettingsEndpoints_TestHelper.CreateSettings(request, _settingsService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<DisplaySettingsDto>>();
        }

        #endregion

        #region Helper Methods

        private static DisplaySettingsDto CreateDisplaySettingsDto(
            long id,
            string name,
            int slideDuration,
            TransitionType transition = TransitionType.Fade,
            int transitionDuration = 1000)
        {
            return new DisplaySettingsDto
            {
                Id = id,
                Name = name,
                SlideDuration = slideDuration,
                Transition = transition,
                TransitionDuration = transitionDuration,
                SourceType = SourceType.All,
                SourceId = null,
                Shuffle = true,
                ImageFit = ImageFit.Contain
            };
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods via reflection.
    /// </summary>
    internal static class DisplaySettingsEndpoints_TestHelper
    {
        public static async Task<Ok<DisplaySettingsDto>> GetActiveSettings(
            IDisplaySettingsService settingsService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("GetActiveSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { settingsService, CancellationToken.None });
            return await (Task<Ok<DisplaySettingsDto>>)result!;
        }

        public static async Task<Ok<IReadOnlyList<DisplaySettingsDto>>> GetAllSettings(
            IDisplaySettingsService settingsService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("GetAllSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { settingsService, CancellationToken.None });
            return await (Task<Ok<IReadOnlyList<DisplaySettingsDto>>>)result!;
        }

        public static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> GetSettingsById(
            long id, IDisplaySettingsService settingsService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("GetSettingsById", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, settingsService, CancellationToken.None });
            return await (Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>>)result!;
        }

        public static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>, BadRequest<ApiError>>> UpdateSettings(
            long id,
            UpdateDisplaySettingsRequest request,
            IDisplaySettingsService settingsService,
            ISlideshowService slideshowService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("UpdateSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, request, settingsService, slideshowService, CancellationToken.None });
            return await (Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>, BadRequest<ApiError>>>)result!;
        }

        public static async Task<Results<Created<DisplaySettingsDto>, BadRequest<ApiError>>> CreateSettings(
            UpdateDisplaySettingsRequest request,
            IDisplaySettingsService settingsService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("CreateSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { request, settingsService, CancellationToken.None });
            return await (Task<Results<Created<DisplaySettingsDto>, BadRequest<ApiError>>>)result!;
        }

        public static async Task<Results<NoContent, NotFound<ApiError>, BadRequest<ApiError>>> DeleteSettings(
            long id, IDisplaySettingsService settingsService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("DeleteSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, settingsService, CancellationToken.None });
            return await (Task<Results<NoContent, NotFound<ApiError>, BadRequest<ApiError>>>)result!;
        }

        public static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> ActivateSettings(
            long id,
            IDisplaySettingsService settingsService,
            ISlideshowService slideshowService)
        {
            var method = typeof(DisplaySettingsEndpoints)
                .GetMethod("ActivateSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, settingsService, slideshowService, CancellationToken.None });
            return await (Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>>)result!;
        }
    }
}
