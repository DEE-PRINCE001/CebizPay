using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Compliance;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class GetDojahWidgetConfigCommandHandlerTests
{
    private readonly IVerificationOrchestrator _orchestrator = Substitute.For<IVerificationOrchestrator>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    [Fact]
    public async Task Handle_CallerIsSelf_DelegatesToOrchestrator()
    {
        // Arrange
        var userId = "user_123";
        _currentUserService.UserId.Returns(userId);

        var expectedDto = new DojahWidgetConfigDto(
            AppId: "test_app_id",
            PublicKey: "test_public_key",
            ReferenceId: "CBZKYC-REF123",
            WidgetType: "custom",
            UserData: new DojahWidgetUserDataDto("Emeka", "Okonkwo", "emeka@example.com", "+2348012345678"),
            EnabledPages: ["government-data", "selfie"]);

        _orchestrator.GetDojahWidgetConfigAsync(userId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var handler = new GetDojahWidgetConfigCommandHandler(_orchestrator, _currentUserService);

        // Act
        var result = await handler.Handle(new GetDojahWidgetConfigCommand(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test_app_id", result.AppId);
        Assert.Equal("test_public_key", result.PublicKey);
        Assert.Equal("CBZKYC-REF123", result.ReferenceId);
        Assert.Equal("Emeka", result.UserData.FirstName);
        await _orchestrator.Received(1).GetDojahWidgetConfigAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        var handler = new GetDojahWidgetConfigCommandHandler(_orchestrator, _currentUserService);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetDojahWidgetConfigCommand(), CancellationToken.None));
    }
}
