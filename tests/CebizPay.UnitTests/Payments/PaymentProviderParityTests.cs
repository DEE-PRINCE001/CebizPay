using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Contract parity tests verifying that both <see cref="FlutterwaveClient"/> and <see cref="PaystackClient"/>
/// produce identical provider-neutral semantic classifications across standard outcome scenarios.
/// </summary>
public sealed class PaymentProviderParityTests
{
    private readonly IOptions<FlutterwaveOptions> _flwOptions = Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions
    {
        BaseUrl = "https://api.flutterwave.com",
        SecretKey = "FLWSECK_TEST-key"
    });

    private readonly IOptions<PaystackOptions> _pstkOptions = Microsoft.Extensions.Options.Options.Create(new PaystackOptions
    {
        BaseUrl = "https://api.paystack.co",
        SecretKey = "sk_test_key"
    });

    [Fact]
    public async Task Parity_SuccessfulTransfer_BothReturnSuccessClassification()
    {
        // Arrange Flutterwave
        var flwHandler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": "success",
            "message": "Transfer Queued",
            "data": { "id": 12345, "status": "NEW", "reference": "REF-001" }
        }
        """);
        var flwClient = new FlutterwaveClient(new HttpClient(flwHandler), _flwOptions, NullLogger<FlutterwaveClient>.Instance);

        // Arrange Paystack
        var pstkHandler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": true,
            "message": "Transfer Queued",
            "data": { "transfer_code": "TRF_12345", "status": "success", "reference": "REF-001" }
        }
        """);
        var pstkClient = new PaystackClient(new HttpClient(pstkHandler), _pstkOptions, NullLogger<PaystackClient>.Instance);

        // Act
        var flwResult = await flwClient.InitiateTransferAsync("044", "0690000031", 1000m, "NGN", "REF-001", "Narration");
        var pstkResult = await pstkClient.InitiateTransferAsync("RCP_123", 1000m, "NGN", "REF-001", "Narration");

        // Assert Parity
        Assert.Equal(PaymentProviderResultStatus.Success, flwResult.Status);
        Assert.Equal(PaymentProviderResultStatus.Success, pstkResult.Status);
        Assert.NotNull(flwResult.ProviderReference);
        Assert.NotNull(pstkResult.ProviderReference);
    }

    [Fact]
    public async Task Parity_BusinessRejection_BothReturnBusinessFailureClassification()
    {
        // Arrange Flutterwave 400
        var flwHandler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, """
        { "status": "error", "message": "Invalid recipient account" }
        """);
        var flwClient = new FlutterwaveClient(new HttpClient(flwHandler), _flwOptions, NullLogger<FlutterwaveClient>.Instance);

        // Arrange Paystack 400
        var pstkHandler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, """
        { "status": false, "message": "Invalid recipient account" }
        """);
        var pstkClient = new PaystackClient(new HttpClient(pstkHandler), _pstkOptions, NullLogger<PaystackClient>.Instance);

        // Act
        var flwResult = await flwClient.InitiateTransferAsync("044", "0000000000", 1000m, "NGN", "REF-002", "Narration");
        var pstkResult = await pstkClient.InitiateTransferAsync("RCP_INVALID", 1000m, "NGN", "REF-002", "Narration");

        // Assert Parity
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, flwResult.Status);
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, pstkResult.Status);
        Assert.Contains("Invalid recipient", flwResult.FailureReason);
        Assert.Contains("Invalid recipient", pstkResult.FailureReason);
    }

    [Fact]
    public async Task Parity_TechnicalError500_BothReturnTechnicalFailureClassification()
    {
        // Arrange Flutterwave 500
        var flwHandler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, """
        { "status": "error", "message": "Internal gateway failure" }
        """);
        var flwClient = new FlutterwaveClient(new HttpClient(flwHandler), _flwOptions, NullLogger<FlutterwaveClient>.Instance);

        // Arrange Paystack 500
        var pstkHandler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, """
        { "status": false, "message": "Server error" }
        """);
        var pstkClient = new PaystackClient(new HttpClient(pstkHandler), _pstkOptions, NullLogger<PaystackClient>.Instance);

        // Act
        var flwResult = await flwClient.InitiateTransferAsync("044", "0690000031", 1000m, "NGN", "REF-003", "Narration");
        var pstkResult = await pstkClient.InitiateTransferAsync("RCP_123", 1000m, "NGN", "REF-003", "Narration");

        // Assert Parity
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, flwResult.Status);
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, pstkResult.Status);
    }

    [Fact]
    public async Task Parity_Timeout_BothReturnUnknownClassification()
    {
        // Arrange Flutterwave timeout
        var flwHandler = new MockHttpMessageHandler(simulateTimeout: true);
        var flwClient = new FlutterwaveClient(new HttpClient(flwHandler), _flwOptions, NullLogger<FlutterwaveClient>.Instance);

        // Arrange Paystack timeout
        var pstkHandler = new MockHttpMessageHandler(simulateTimeout: true);
        var pstkClient = new PaystackClient(new HttpClient(pstkHandler), _pstkOptions, NullLogger<PaystackClient>.Instance);

        // Act
        var flwResult = await flwClient.InitiateTransferAsync("044", "0690000031", 1000m, "NGN", "REF-004", "Narration");
        var pstkResult = await pstkClient.InitiateTransferAsync("RCP_123", 1000m, "NGN", "REF-004", "Narration");

        // Assert Parity
        Assert.Equal(PaymentProviderResultStatus.Unknown, flwResult.Status);
        Assert.Equal(PaymentProviderResultStatus.Unknown, pstkResult.Status);
    }
}
