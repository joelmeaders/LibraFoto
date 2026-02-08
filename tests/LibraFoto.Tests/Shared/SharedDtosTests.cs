namespace LibraFoto.Tests.Shared
{
    /// <summary>
    /// Tests for Shared DTOs (PagedResult, ApiError, PaginationInfo).
    /// </summary>
    public class SharedDtosTests
    {
        [Test]
        public async Task PagedResult_Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange
            var data = new[] { 1, 2, 3 };
            var pagination = new LibraFoto.Shared.DTOs.PaginationInfo(1, 10, 3, 1);

            // Act
            var result = new LibraFoto.Shared.DTOs.PagedResult<int>(data, pagination);

            // Assert
            await Assert.That(result.Data).IsEquivalentTo(data);
            await Assert.That(result.Pagination).IsEqualTo(pagination);
        }

        [Test]
        public async Task ApiError_Constructor_InitializesPropertiesCorrectly()
        {
            // Act
            var error = new LibraFoto.Shared.DTOs.ApiError("NOT_FOUND", "Resource not found");

            // Assert
            await Assert.That(error.Code).IsEqualTo("NOT_FOUND");
            await Assert.That(error.Message).IsEqualTo("Resource not found");
            await Assert.That(error.Details).IsNull();
        }

        [Test]
        public async Task ApiError_WithDetails_InitializesAllProperties()
        {
            // Arrange
            var details = new { Field = "email", Reason = "Invalid format" };

            // Act
            var error = new LibraFoto.Shared.DTOs.ApiError("VALIDATION_ERROR", "Validation failed", details);

            // Assert
            await Assert.That(error.Code).IsEqualTo("VALIDATION_ERROR");
            await Assert.That(error.Message).IsEqualTo("Validation failed");
            await Assert.That(error.Details).IsNotNull();
        }

        [Test]
        public async Task PaginationInfo_Constructor_StoresAllProperties()
        {
            // Act
            var pagination = new LibraFoto.Shared.DTOs.PaginationInfo(2, 10, 25, 3);

            // Assert
            await Assert.That(pagination.Page).IsEqualTo(2);
            await Assert.That(pagination.PageSize).IsEqualTo(10);
            await Assert.That(pagination.TotalItems).IsEqualTo(25);
            await Assert.That(pagination.TotalPages).IsEqualTo(3);
        }

        [Test]
        public async Task PaginationInfo_WithZeroTotal_HasZeroPages()
        {
            // Act
            var pagination = new LibraFoto.Shared.DTOs.PaginationInfo(1, 10, 0, 0);

            // Assert
            await Assert.That(pagination.TotalItems).IsEqualTo(0);
            await Assert.That(pagination.TotalPages).IsEqualTo(0);
        }
    }
}
