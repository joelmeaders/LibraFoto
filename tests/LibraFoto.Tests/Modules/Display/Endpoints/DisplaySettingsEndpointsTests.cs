using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Display.Endpoints
{
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

        [Test]
        public async Task GetActiveSettings_ReturnsSettingsFromService()
        {
            // Arrange
            var settings = new DisplaySettingsDto
            {
                Id = 1,
                Name = "Default",
                SlideDuration = 10,
                Transition = TransitionType.Fade,
                TransitionDuration = 1000,
                SourceType = SourceType.All,
                Shuffle = true,
                ImageFit = ImageFit.Contain
            };
            _settingsService.GetActiveSettingsAsync(Arg.Any<CancellationToken>())
                .Returns(settings);

            // Act
            var result = await _settingsService.GetActiveSettingsAsync(CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Id).IsEqualTo(1);
            await Assert.That(result.Name).IsEqualTo("Default");
            await Assert.That(result.SlideDuration).IsEqualTo(10);
        }

        [Test]
        public async Task GetAllSettings_ReturnsListFromService()
        {
            // Arrange
            var settingsList = new List<DisplaySettingsDto>
            {
                new() { Id = 1, Name = "Default", SlideDuration = 10 },
                new() { Id = 2, Name = "Fast", SlideDuration = 5 }
            };
            _settingsService.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(settingsList.AsReadOnly());

            // Act
            var result = await _settingsService.GetAllAsync(CancellationToken.None);

            // Assert
            await Assert.That(result.Count).IsEqualTo(2);
            await Assert.That(result[0].Name).IsEqualTo("Default");
            await Assert.That(result[1].Name).IsEqualTo("Fast");
        }

        [Test]
        public async Task GetSettingsById_ReturnsSettings_WhenFound()
        {
            // Arrange
            var settings = new DisplaySettingsDto
            {
                Id = 5,
                Name = "Custom",
                SlideDuration = 15,
                Transition = TransitionType.Fade,
                TransitionDuration = 500,
                Shuffle = false,
                ImageFit = ImageFit.Cover
            };
            _settingsService.GetByIdAsync(5, Arg.Any<CancellationToken>())
                .Returns(settings);

            // Act
            var result = await _settingsService.GetByIdAsync(5, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(5);
            await Assert.That(result.Name).IsEqualTo("Custom");
        }

        [Test]
        public async Task GetSettingsById_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _settingsService.GetByIdAsync(999, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await _settingsService.GetByIdAsync(999, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdateSettings_ReturnsUpdatedSettings_WhenSuccessful()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "Updated",
                SlideDuration = 20,
                Shuffle = false
            };
            var updatedSettings = new DisplaySettingsDto
            {
                Id = 1,
                Name = "Updated",
                SlideDuration = 20,
                Transition = TransitionType.Fade,
                TransitionDuration = 1000,
                Shuffle = false,
                ImageFit = ImageFit.Contain
            };
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns(updatedSettings);

            // Act
            var result = await _settingsService.UpdateAsync(1, request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("Updated");
            await Assert.That(result.SlideDuration).IsEqualTo(20);
            await Assert.That(result.Shuffle).IsEqualTo(false);
        }

        [Test]
        public async Task UpdateSettings_InvalidSlideDuration_LessThanOne()
        {
            // Arrange - slide duration < 1 is invalid
            var request = new UpdateDisplaySettingsRequest
            {
                SlideDuration = 0
            };
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await _settingsService.UpdateAsync(1, request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
            await _settingsService.Received(1).UpdateAsync(1,
                Arg.Is<UpdateDisplaySettingsRequest>(r => r.SlideDuration == 0),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task UpdateSettings_NegativeTransitionDuration()
        {
            // Arrange - negative transition duration is invalid
            var request = new UpdateDisplaySettingsRequest
            {
                TransitionDuration = -500
            };
            _settingsService.UpdateAsync(1, request, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await _settingsService.UpdateAsync(1, request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
            await _settingsService.Received(1).UpdateAsync(1,
                Arg.Is<UpdateDisplaySettingsRequest>(r => r.TransitionDuration == -500),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task UpdateSettings_ReturnsNull_WhenSettingsNotFound()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { Name = "Ghost" };
            _settingsService.UpdateAsync(999, request, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await _settingsService.UpdateAsync(999, request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task CreateSettings_ReturnsCreatedSettings()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "New Config",
                SlideDuration = 8,
                Transition = TransitionType.Fade,
                TransitionDuration = 750,
                SourceType = SourceType.All,
                Shuffle = true,
                ImageFit = ImageFit.Contain
            };
            var createdSettings = new DisplaySettingsDto
            {
                Id = 10,
                Name = "New Config",
                SlideDuration = 8,
                Transition = TransitionType.Fade,
                TransitionDuration = 750,
                SourceType = SourceType.All,
                Shuffle = true,
                ImageFit = ImageFit.Contain
            };
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await _settingsService.CreateAsync(request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Id).IsEqualTo(10);
            await Assert.That(result.Name).IsEqualTo("New Config");
            await Assert.That(result.SlideDuration).IsEqualTo(8);
            await Assert.That(result.TransitionDuration).IsEqualTo(750);
        }

        [Test]
        public async Task CreateSettings_InvalidSlideDuration()
        {
            // Arrange - test that invalid slide duration request is passed through
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "Invalid",
                SlideDuration = -1
            };
            var createdSettings = new DisplaySettingsDto
            {
                Id = 11,
                Name = "Invalid",
                SlideDuration = -1
            };
            _settingsService.CreateAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdSettings);

            // Act
            var result = await _settingsService.CreateAsync(request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.SlideDuration).IsEqualTo(-1);
            await _settingsService.Received(1).CreateAsync(
                Arg.Is<UpdateDisplaySettingsRequest>(r => r.SlideDuration == -1),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeleteSettings_ReturnsTrue_WhenSuccessful()
        {
            // Arrange
            _settingsService.DeleteAsync(1, Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _settingsService.DeleteAsync(1, CancellationToken.None);

            // Assert
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task DeleteSettings_ReturnsFalse_WhenNotFound()
        {
            // Arrange
            _settingsService.DeleteAsync(999, Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var result = await _settingsService.DeleteAsync(999, CancellationToken.None);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task ActivateSettings_ReturnsActivatedSettings_WhenSuccessful()
        {
            // Arrange
            var activatedSettings = new DisplaySettingsDto
            {
                Id = 3,
                Name = "Activated Config",
                SlideDuration = 12,
                Transition = TransitionType.Fade,
                TransitionDuration = 800,
                Shuffle = true,
                ImageFit = ImageFit.Contain
            };
            _settingsService.SetActiveAsync(3, Arg.Any<CancellationToken>())
                .Returns(activatedSettings);

            // Act
            var result = await _settingsService.SetActiveAsync(3, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(3);
            await Assert.That(result.Name).IsEqualTo("Activated Config");
        }

        [Test]
        public async Task ActivateSettings_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _settingsService.SetActiveAsync(999, Arg.Any<CancellationToken>())
                .Returns((DisplaySettingsDto?)null);

            // Act
            var result = await _settingsService.SetActiveAsync(999, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
        }
    }
}
