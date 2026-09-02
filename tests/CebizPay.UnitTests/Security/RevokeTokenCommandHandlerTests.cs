using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Auth.RevokeToken;
using NSubstitute;

namespace CebizPay.UnitTests.Security;

public sealed class RevokeTokenCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly RevokeTokenCommandHandler _handler;

    public RevokeTokenCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _handler = new RevokeTokenCommandHandler(_identityService);
    }

    [Fact]
    public async Task Handle_WhenRevocationSucceeds_ShouldReturnSuccessfulDto()
    {
        // Arrange
        var command = new RevokeTokenCommand("token_to_revoke_123");
        _identityService.RevokeRefreshTokenAsync("token_to_revoke_123", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Token revoked successfully.", result.Message);
    }

    [Fact]
    public async Task Handle_WhenRevocationFails_ShouldReturnFailureDto()
    {
        // Arrange
        var command = new RevokeTokenCommand("");
        _identityService.RevokeRefreshTokenAsync("", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Token could not be revoked or was not found.", result.Message);
    }
}
