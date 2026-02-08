using LibraFoto.Modules.Admin.Endpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Admin.Endpoints
{
    /// <summary>
    /// Comprehensive tests for SystemEndpoints covering all endpoint methods.
    /// </summary>
    public class SystemEndpointsTests
    {
        private ISystemService _systemService = null!;

        [Before(Test)]
        public void Setup()
        {
            _systemService = Substitute.For<ISystemService>();
        }

        #region GetSystemInfo Tests

        [Test]
        public async Task GetSystemInfo_ReturnsSystemInfo()
        {
            // Arrange
            var expectedInfo = new SystemInfoResponse
            {
                Version = "1.0.0",
                Environment = "Test",
                Uptime = TimeSpan.FromHours(2),
                UpdateAvailable = false,
                LatestVersion = null,
                IsDocker = false
            };
            _systemService.GetSystemInfoAsync(Arg.Any<CancellationToken>()).Returns(expectedInfo);

            // Act
            var method = typeof(SystemEndpoints).GetMethod("GetSystemInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<SystemInfoResponse>>)method!.Invoke(null, new object[] { _systemService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value).IsEqualTo(expectedInfo);
            await Assert.That(result.Value!.Version).IsEqualTo("1.0.0");
            await Assert.That(result.Value.Uptime).IsEqualTo(TimeSpan.FromHours(2));
        }

        #endregion

        #region CheckForUpdates Tests

        [Test]
        public async Task CheckForUpdates_ReturnsUpdateCheckResponse()
        {
            // Arrange
            var expectedResponse = new UpdateCheckResponse
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.1.0",
                UpdateAvailable = true,
                CommitsBehind = 5,
                Changelog = new[] { "New features" }
            };
            _systemService.CheckForUpdatesAsync(false, Arg.Any<CancellationToken>()).Returns(expectedResponse);

            // Act
            var method = typeof(SystemEndpoints).GetMethod("CheckForUpdates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<UpdateCheckResponse>>)method!.Invoke(null, new object[] { _systemService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value).IsEqualTo(expectedResponse);
            await Assert.That(result.Value!.UpdateAvailable).IsTrue();
            await Assert.That(result.Value.LatestVersion).IsEqualTo("1.1.0");
        }

        [Test]
        public async Task CheckForUpdates_NoUpdateAvailable_ReturnsFalse()
        {
            // Arrange
            var expectedResponse = new UpdateCheckResponse
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.0.0",
                UpdateAvailable = false,
                CommitsBehind = 0
            };
            _systemService.CheckForUpdatesAsync(false, Arg.Any<CancellationToken>()).Returns(expectedResponse);

            // Act
            var method = typeof(SystemEndpoints).GetMethod("CheckForUpdates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<UpdateCheckResponse>>)method!.Invoke(null, new object[] { _systemService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.UpdateAvailable).IsFalse();
            await Assert.That(result.Value.CurrentVersion).IsEqualTo(result.Value.LatestVersion);
        }

        #endregion

        #region ForceCheckForUpdates Tests

        [Test]
        public async Task ForceCheckForUpdates_ForcesRefresh()
        {
            // Arrange
            var expectedResponse = new UpdateCheckResponse
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.2.0",
                UpdateAvailable = true,
                CommitsBehind = 10,
                Changelog = new[] { "Major update" }
            };
            _systemService.CheckForUpdatesAsync(true, Arg.Any<CancellationToken>()).Returns(expectedResponse);

            // Act
            var method = typeof(SystemEndpoints).GetMethod("ForceCheckForUpdates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<UpdateCheckResponse>>)method!.Invoke(null, new object[] { _systemService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value).IsEqualTo(expectedResponse);
            await _systemService.Received(1).CheckForUpdatesAsync(true, Arg.Any<CancellationToken>());
        }

        #endregion

        #region TriggerUpdate Tests

        [Test]
        public async Task TriggerUpdate_ReturnsAcceptedResponse()
        {
            // Arrange
            var expectedResponse = new UpdateTriggerResponse(
                "Update triggered successfully",
                60
            );
            _systemService.TriggerUpdateAsync(Arg.Any<CancellationToken>()).Returns(expectedResponse);

            // Act
            var method = typeof(SystemEndpoints).GetMethod("TriggerUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Accepted<UpdateTriggerResponse>>)method!.Invoke(null, new object[] { _systemService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value).IsEqualTo(expectedResponse);
            await Assert.That(result.Value!.Message).IsEqualTo("Update triggered successfully");
        }

        [Test]
        public async Task TriggerUpdate_WhenFails_ReturnsFailureResponse()
        {
            // Arrange
            var expectedResponse = new UpdateTriggerResponse(
                "Update failed: No update available",
                0
            );
            _systemService.TriggerUpdateAsync(Arg.Any<CancellationToken>()).Returns(expectedResponse);

            // Act
            var method = typeof(SystemEndpoints).GetMethod("TriggerUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Accepted<UpdateTriggerResponse>>)method!.Invoke(null, new object[] { _systemService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Message).Contains("failed");
            await Assert.That(result.Value.EstimatedDowntimeSeconds).IsEqualTo(0);
        }

        #endregion
    }
}
