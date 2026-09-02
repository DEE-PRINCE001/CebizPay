using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CebizPay.UnitTests.Security;

public sealed class EmailAndSmsMfaCodeDeliveryServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly EmailAndSmsMfaCodeDeliveryService _service;

    public EmailAndSmsMfaCodeDeliveryServiceTests()
    {
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(userStore, null, null, null, null, null, null, null, null);
        _emailService = Substitute.For<IEmailService>();
        _smsService = Substitute.For<ISmsService>();

        _service = new EmailAndSmsMfaCodeDeliveryService(
            _userManager,
            _emailService,
            _smsService,
            NullLogger<EmailAndSmsMfaCodeDeliveryService>.Instance);
    }

    [Fact]
    public async Task DeliverAsync_WhenUserHasEmailAndPhone_ShouldSendBothEmailAndSms()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = "admin@example.com",
            PhoneNumber = "+2348012345678"
        };
        _userManager.FindByIdAsync("user-123").Returns(user);

        // Act
        await _service.DeliverAsync("user-123", "481920");

        // Assert
        await _emailService.Received(1).SendEmailAsync(
            "admin@example.com",
            Arg.Is<string>(s => s.Contains("MFA") || s.Contains("Authentication")),
            Arg.Is<string>(b => b.Contains("481920")),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _smsService.Received(1).SendSmsAsync(
            "+2348012345678",
            Arg.Is<string>(m => m.Contains("481920")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliverAsync_WhenUserNotFound_ShouldNotSendEmailOrSms()
    {
        // Arrange
        _userManager.FindByIdAsync("nonexistent").Returns((ApplicationUser)null!);

        // Act
        await _service.DeliverAsync("nonexistent", "481920");

        // Assert
        await _emailService.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _smsService.DidNotReceive().SendSmsAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
