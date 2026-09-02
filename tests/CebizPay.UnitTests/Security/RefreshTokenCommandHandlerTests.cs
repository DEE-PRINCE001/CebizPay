using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Auth.RefreshToken;
using NSubstitute;

namespace CebizPay.UnitTests.Security;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _handler = new RefreshTokenCommandHandler(_identityService);
    }

    [Fact]
    public async Task Handle_WhenServiceSucceeds_ShouldReturnSuccessfulDto()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid_refresh_token_123", "127.0.0.1");
        _identityService.RefreshTokenAsync("valid_refresh_token_123", "127.0.0.1", Arg.Any<CancellationToken>())
            .Returns((true, "user_456", "new_jwt_access_token", "new_refresh_token_789", (string?)null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("user_456", result.UserId);
        Assert.Equal("new_jwt_access_token", result.AccessToken);
        Assert.Equal("new_refresh_token_789", result.RefreshToken);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenServiceFails_ShouldReturnFailureDtoWithErrorMessage()
    {
        // Arrange
        var command = new RefreshTokenCommand("expired_refresh_token", "127.0.0.1");
        _identityService.RefreshTokenAsync("expired_refresh_token", "127.0.0.1", Arg.Any<CancellationToken>())
            .Returns((false, string.Empty, string.Empty, string.Empty, "Refresh token has expired. Please log in again."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.AccessToken);
        Assert.Null(result.RefreshToken);
        Assert.Equal("Refresh token has expired. Please log in again.", result.ErrorMessage);
    }
}
