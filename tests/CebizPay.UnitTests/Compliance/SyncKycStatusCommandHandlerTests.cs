using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Compliance;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class SyncKycStatusCommandHandlerTests
{
    private readonly IVerificationOrchestrator _orchestrator = Substitute.For<IVerificationOrchestrator>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SyncKycStatusCommandValidator _validator = new();

    [Fact]
    public void Validator_EmptyReferenceId_ShouldFail()
    {
        var command = new SyncKycStatusCommand("");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ReferenceId);
    }

    [Fact]
    public void Validator_ValidReferenceId_ShouldPass()
    {
        var command = new SyncKycStatusCommand("CBZKYC-123456");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Handle_CallerIsSelf_DelegatesToOrchestrator()
    {
        var userId = "user_123";
        const string refId = "CBZKYC-REF123";
        _currentUserService.UserId.Returns(userId);

        var expectedDto = new KycSyncResultDto(
            ReferenceId: refId,
            Status: "Verified",
            Message: "KYC verification synchronized and verified successfully.",
            KycTier: "Tier2",
            VirtualAccountNumber: "9876543210",
            BankName: "Wema Bank / Monnify");

        _orchestrator.SyncKycVerificationAsync(userId, refId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var handler = new SyncKycStatusCommandHandler(_orchestrator, _currentUserService);

        var result = await handler.Handle(new SyncKycStatusCommand(refId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Verified", result.Status);
        Assert.Equal("9876543210", result.VirtualAccountNumber);
        await _orchestrator.Received(1).SyncKycVerificationAsync(userId, refId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        _currentUserService.UserId.Returns((string?)null);
        var handler = new SyncKycStatusCommandHandler(_orchestrator, _currentUserService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new SyncKycStatusCommand("CBZKYC-123"), CancellationToken.None));
    }
}
