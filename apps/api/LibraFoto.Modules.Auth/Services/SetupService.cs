using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Auth.Services
{
    /// <summary>
    /// Setup service implementation for initial application setup.
    /// </summary>
    public class SetupService : ISetupService
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly ILogger<SetupService> _logger;

        public SetupService(
            IUserService userService,
            IAuthService authService,
            ILogger<SetupService> logger)
        {
            _userService = userService;
            _authService = authService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
        {
            var userCount = await _userService.GetUserCountAsync(cancellationToken);
            return userCount == 0;
        }

        /// <inheritdoc />
        public async Task<LoginResponse?> CompleteSetupAsync(SetupRequest request, CancellationToken cancellationToken = default)
        {
            // Check if setup is still required
            if (!await IsSetupRequiredAsync(cancellationToken))
            {
                _logger.LogWarning("Setup attempted when users already exist");
                return null;
            }

            // Create the first admin user
            var createUserRequest = new CreateUserRequest(
                request.Email,
                request.Password,
                UserRole.Admin);

            var user = await _userService.CreateUserAsync(createUserRequest, cancellationToken);

            _logger.LogInformation("Initial setup completed. Created admin user: {Email}", user.Email);

            // Log in the new admin user and return the login response
            var loginRequest = new LoginRequest(request.Email, request.Password);
            return await _authService.LoginAsync(loginRequest, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken = default)
        {
            var isRequired = await IsSetupRequiredAsync(cancellationToken);

            return new SetupStatusResponse(
                isRequired,
                isRequired
                    ? "Initial setup required. Please create the first admin user."
                    : "Setup has been completed."
            );
        }
    }
}
