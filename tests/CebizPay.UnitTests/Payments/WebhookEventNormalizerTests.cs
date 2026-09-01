using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using Xunit;

namespace CebizPay.UnitTests.Payments;

public sealed class WebhookEventNormalizerTests
{
    private readonly WebhookEventNormalizer _normalizer = new();

    [Fact]
    public void NormalizeFinancial_MonnifySuccessPayload_ExtractsCorrectFields()
    {
        var rawPayload = """
        {
            "eventType": "SUCCESSFUL_TRANSACTION",
            "eventData": {
                "transactionReference": "MNFY|20260901|001",
                "paymentReference": "CBZ-VA-12345",
                "amountPaid": 25000.00,
                "totalPayable": 25000.00,
                "paymentStatus": "PAID",
                "currencyCode": "NGN",
                "destinationAccountNumber": "9988776655"
            }
        }
        """;

        var result = _normalizer.NormalizeFinancial(PaymentProvider.Monnify, rawPayload, new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(PaymentProvider.Monnify, result.Provider);
        Assert.Equal(NormalizedWebhookOutcome.Success, result.Outcome);
        Assert.Equal(25000.00m, result.Amount);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal("CBZ-VA-12345", result.InternalReference);
        Assert.Equal("MNFY|20260901|001", result.ProviderReference);
        Assert.Equal("9988776655", result.AccountNumber);
    }

    [Fact]
    public void NormalizeFinancial_FlutterwaveTransferCompleted_ExtractsCorrectFields()
    {
        var rawPayload = """
        {
            "event": "transfer.completed",
            "data": {
                "id": 987654321,
                "reference": "CBZBT-FLW-999",
                "tx_ref": "FLW_TXN_001",
                "amount": 50000.00,
                "currency": "NGN",
                "status": "SUCCESSFUL",
                "complete_message": "Transfer processed successfully"
            }
        }
        """;

        var result = _normalizer.NormalizeFinancial(PaymentProvider.Flutterwave, rawPayload, new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(PaymentProvider.Flutterwave, result.Provider);
        Assert.Equal(NormalizedWebhookOutcome.Success, result.Outcome);
        Assert.Equal(50000.00m, result.Amount);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal("CBZBT-FLW-999", result.InternalReference);
        Assert.Equal("987654321", result.ProviderReference);
    }

    [Fact]
    public void NormalizeFinancial_PaystackTransferFailed_ExtractsCorrectFailure()
    {
        var rawPayload = """
        {
            "event": "transfer.failed",
            "data": {
                "transfer_code": "TRF_abc123xyz",
                "reference": "CBZBT-PSTK-001",
                "amount": 1500000,
                "currency": "NGN",
                "status": "failed",
                "reason": "Destination account blocked"
            }
        }
        """;

        var result = _normalizer.NormalizeFinancial(PaymentProvider.Paystack, rawPayload, new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(PaymentProvider.Paystack, result.Provider);
        Assert.Equal(NormalizedWebhookOutcome.Failure, result.Outcome);
        Assert.Equal(15000.00m, result.Amount);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal("CBZBT-PSTK-001", result.InternalReference);
        Assert.Equal("TRF_abc123xyz", result.ProviderReference);
    }

    [Fact]
    public void NormalizeCompliance_DojahMatch_ExtractsCorrectComplianceEvent()
    {
        var rawPayload = """
        {
            "event": "kyc_bvn_verify",
            "id": "dj_evt_12345",
            "data": {
                "reference_id": "DOJAH_REF_001",
                "status": "MATCH"
            }
        }
        """;

        var result = _normalizer.NormalizeCompliance(VerificationProvider.Dojah, rawPayload, new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(VerificationProvider.Dojah, result.Provider);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
        Assert.Equal("DOJAH_REF_001", result.VerificationReference);
    }

    [Fact]
    public void NormalizeCompliance_SmileIdJobComplete_ExtractsCorrectStatus()
    {
        var rawPayload = """
        {
            "event_type": "job_complete",
            "SmileJobID": "SMILE_JOB_999",
            "ResultCode": "0810",
            "ResultText": "Approved and Verified",
            "PartnerParams": {
                "job_id": "CBZ-KYC-SM-123"
            }
        }
        """;

        var result = _normalizer.NormalizeCompliance(VerificationProvider.SmileId, rawPayload, new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(VerificationProvider.SmileId, result.Provider);
        Assert.Equal(VerificationResultStatus.Match, result.ResultStatus);
        Assert.Equal("CBZ-KYC-SM-123", result.VerificationReference);
        Assert.Equal("SMILE_JOB_999", result.ProviderReference);
    }

    [Fact]
    public void NormalizeCompliance_NinjaMismatch_ExtractsMismatchResult()
    {
        var rawPayload = """
        {
            "type": "cac.verification",
            "id": "nin_evt_001",
            "data": {
                "reference": "NINJA_CAC_RC123",
                "status": "mismatch"
            }
        }
        """;

        var result = _normalizer.NormalizeCompliance(VerificationProvider.Ninja, rawPayload, new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(VerificationProvider.Ninja, result.Provider);
        Assert.Equal(VerificationResultStatus.Mismatch, result.ResultStatus);
        Assert.Equal("NINJA_CAC_RC123", result.VerificationReference);
    }
}
