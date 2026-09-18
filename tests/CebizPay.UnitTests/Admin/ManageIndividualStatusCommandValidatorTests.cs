using CebizPay.Application.UseCases.Admin.Individuals;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class ManageIndividualStatusCommandValidatorTests
{
    private readonly SuspendIndividualCommandValidator _suspendValidator = new();
    private readonly ReactivateIndividualCommandValidator _reactivateValidator = new();

    [Fact]
    public void SuspendCommand_WhenValid_PassesValidation()
    {
        var command = new SuspendIndividualCommand("user-123", "Valid regulatory suspension reason", "admin-456");
        var result = _suspendValidator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void SuspendCommand_WhenIdEmpty_FailsValidation(string? invalidId)
    {
        var command = new SuspendIndividualCommand(invalidId!, "Valid reason", "admin-456");
        var result = _suspendValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SuspendIndividualCommand.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1234")] // Less than 5 characters
    public void SuspendCommand_WhenReasonTooShort_FailsValidation(string invalidReason)
    {
        var command = new SuspendIndividualCommand("user-123", invalidReason, "admin-456");
        var result = _suspendValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SuspendIndividualCommand.Reason));
    }

    [Fact]
    public void SuspendCommand_WhenSelfSuspension_FailsValidation()
    {
        var command = new SuspendIndividualCommand("admin-456", "Valid reason", "admin-456");
        var result = _suspendValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Administrators cannot suspend their own individual account"));
    }

    [Fact]
    public void ReactivateCommand_WhenValid_PassesValidation()
    {
        var command = new ReactivateIndividualCommand("user-123", "Compliance check passed and account cleared", "admin-456");
        var result = _reactivateValidator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ReactivateCommand_WhenIdEmpty_FailsValidation(string? invalidId)
    {
        var command = new ReactivateIndividualCommand(invalidId!, "Valid reason", "admin-456");
        var result = _reactivateValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReactivateIndividualCommand.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("123")] // Less than 5 characters
    public void ReactivateCommand_WhenReasonTooShort_FailsValidation(string invalidReason)
    {
        var command = new ReactivateIndividualCommand("user-123", invalidReason, "admin-456");
        var result = _reactivateValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReactivateIndividualCommand.Reason));
    }
}
