#pragma warning disable CA1848, CS1591
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure normalizer converting raw provider payloads (Monnify, Flutterwave, Paystack,
/// Dojah, Smile ID, Ninja) into unified, strongly typed neutral domain representations.
/// </summary>
public sealed class WebhookEventNormalizer : IWebhookEventNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc/>
    public NormalizedWebhookEvent? NormalizeFinancial(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            return provider switch
            {
                PaymentProvider.Monnify => NormalizeMonnify(root, rawPayload),
                PaymentProvider.Flutterwave => NormalizeFlutterwave(root, rawPayload),
                PaymentProvider.Paystack => NormalizePaystack(root, rawPayload),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public NormalizedComplianceWebhookEvent? NormalizeCompliance(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            return provider switch
            {
                VerificationProvider.Dojah => NormalizeDojah(root, rawPayload),
                VerificationProvider.SmileId => NormalizeSmileId(root, rawPayload),
                VerificationProvider.Ninja => NormalizeNinja(root, rawPayload),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static NormalizedWebhookEvent? NormalizeMonnify(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() : null;
        if (string.IsNullOrWhiteSpace(eventType))
            eventType = root.TryGetProperty("event_type", out var et2) ? et2.GetString() : "monnify.notification";

        if (!root.TryGetProperty("eventData", out var data) && !root.TryGetProperty("responseBody", out data))
        {
            data = root;
        }

        var txRef = data.TryGetProperty("transactionReference", out var tr) ? tr.GetString() : null;
        var payRef = data.TryGetProperty("paymentReference", out var pr) ? pr.GetString() : null;
        var refProp = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var effectiveRef = payRef ?? txRef ?? refProp;

        var accountNumber = data.TryGetProperty("destinationAccountNumber", out var da) ? da.GetString() : null;
        if (string.IsNullOrWhiteSpace(accountNumber))
            accountNumber = data.TryGetProperty("accountNumber", out var an) ? an.GetString() : null;

        var paymentStatus = data.TryGetProperty("paymentStatus", out var ps) ? ps.GetString()?.ToUpperInvariant() : null;
        if (string.IsNullOrWhiteSpace(paymentStatus))
            paymentStatus = data.TryGetProperty("status", out var st) ? st.GetString()?.ToUpperInvariant() : null;

        var currency = data.TryGetProperty("currencyCode", out var cc) ? cc.GetString() : "NGN";
        if (string.IsNullOrWhiteSpace(currency))
            currency = data.TryGetProperty("currency", out var curr) ? curr.GetString() : "NGN";

        decimal? amount = null;
        if (data.TryGetProperty("amountPaid", out var ap) && ap.ValueKind == JsonValueKind.Number)
            amount = ap.GetDecimal();
        else if (data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number)
            amount = am.GetDecimal();
        else if (data.TryGetProperty("totalPayable", out var tp) && tp.ValueKind == JsonValueKind.Number)
            amount = tp.GetDecimal();

        var outcome = paymentStatus switch
        {
            "PAID" or "SUCCESS" or "SUCCESSFUL" => NormalizedWebhookOutcome.Success,
            "FAILED" or "EXPIRED" or "CANCELLED" => NormalizedWebhookOutcome.Failure,
            "REVERSED" => NormalizedWebhookOutcome.Reversed,
            "PENDING" or "PROCESSING" => NormalizedWebhookOutcome.Pending,
            _ => (eventType != null && eventType.Contains("SUCCESSFUL", StringComparison.OrdinalIgnoreCase)) ? NormalizedWebhookOutcome.Success :
                 (eventType != null && eventType.Contains("FAILED", StringComparison.OrdinalIgnoreCase)) ? NormalizedWebhookOutcome.Failure :
                 NormalizedWebhookOutcome.Unknown
        };

        var providerEventId = !string.IsNullOrWhiteSpace(txRef)
            ? string.Format(CultureInfo.InvariantCulture, "mnfy_evt_{0}_{1}_{2}", eventType, txRef, paymentStatus ?? "unknown")
            : !string.IsNullOrWhiteSpace(effectiveRef)
                ? string.Format(CultureInfo.InvariantCulture, "mnfy_evt_{0}_{1}_{2}", eventType, effectiveRef, paymentStatus ?? "unknown")
                : ComputeHash(rawPayload);

        var safeMeta = JsonSerializer.Serialize(new
        {
            transaction_reference = txRef,
            payment_reference = payRef,
            payment_status = paymentStatus,
            event_type = eventType,
            account_number = accountNumber
        });

        return new NormalizedWebhookEvent(
            Provider: PaymentProvider.Monnify,
            EventType: eventType ?? "monnify.notification",
            ProviderEventId: providerEventId,
            InternalReference: effectiveRef,
            ProviderReference: txRef ?? payRef ?? refProp,
            AccountNumber: accountNumber,
            Amount: amount,
            Currency: currency,
            Outcome: outcome,
            FailureCode: outcome == NormalizedWebhookOutcome.Failure ? "DISBURSEMENT_FAILED" : null,
            FailureReason: outcome == NormalizedWebhookOutcome.Failure ? "Monnify reported payment as failed or expired" : null,
            OccurredAtUtc: DateTime.UtcNow,
            SafeMetadata: safeMeta);
    }

    private static NormalizedWebhookEvent? NormalizeFlutterwave(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : "transfer.completed";
        if (string.IsNullOrWhiteSpace(eventType)) eventType = "transfer.completed";

        if (!root.TryGetProperty("data", out var data))
            return null;

        long id = 0;
        if (data.TryGetProperty("id", out var idProp))
        {
            if (idProp.ValueKind == JsonValueKind.Number) id = idProp.GetInt64();
            else if (idProp.ValueKind == JsonValueKind.String && long.TryParse(idProp.GetString(), CultureInfo.InvariantCulture, out var parsedId)) id = parsedId;
        }

        var status = data.TryGetProperty("status", out var st) ? st.GetString()?.ToUpperInvariant() : null;
        var reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var txRef = data.TryGetProperty("tx_ref", out var tr) ? tr.GetString() : null;
        var effectiveRef = reference ?? txRef;

        var accountNumber = data.TryGetProperty("account_number", out var an) ? an.GetString() : null;
        var currency = data.TryGetProperty("currency", out var curr) ? curr.GetString() : "NGN";
        decimal? amount = data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number ? am.GetDecimal() : null;
        var completeMessage = data.TryGetProperty("complete_message", out var cm) ? cm.GetString() : null;

        var outcome = status switch
        {
            "SUCCESSFUL" => NormalizedWebhookOutcome.Success,
            "FAILED" => NormalizedWebhookOutcome.Failure,
            "PENDING" or "NEW" => NormalizedWebhookOutcome.Pending,
            _ => NormalizedWebhookOutcome.Unknown
        };

        var providerEventId = id > 0
            ? string.Format(CultureInfo.InvariantCulture, "flw_evt_{0}_{1}", id, status ?? "unknown")
            : ComputeHash(rawPayload);

        var safeMeta = JsonSerializer.Serialize(new
        {
            provider_id = id,
            status = status,
            reference = effectiveRef,
            account_number = accountNumber
        });

        return new NormalizedWebhookEvent(
            Provider: PaymentProvider.Flutterwave,
            EventType: eventType,
            ProviderEventId: providerEventId,
            InternalReference: effectiveRef,
            ProviderReference: id > 0 ? id.ToString(CultureInfo.InvariantCulture) : effectiveRef,
            AccountNumber: accountNumber,
            Amount: amount,
            Currency: currency,
            Outcome: outcome,
            FailureCode: outcome == NormalizedWebhookOutcome.Failure ? "TRANSFER_FAILED" : null,
            FailureReason: completeMessage ?? (outcome == NormalizedWebhookOutcome.Failure ? "Transfer rejected by gateway" : null),
            OccurredAtUtc: DateTime.UtcNow,
            SafeMetadata: safeMeta);
    }

    private static NormalizedWebhookEvent? NormalizePaystack(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : "transfer.success";
        if (string.IsNullOrWhiteSpace(eventType)) eventType = "transfer.unknown";

        if (!root.TryGetProperty("data", out var data))
            return null;

        var transferCode = data.TryGetProperty("transfer_code", out var tc) ? tc.GetString() : null;
        var reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var status = data.TryGetProperty("status", out var st) ? st.GetString()?.ToLowerInvariant() : null;
        var currency = data.TryGetProperty("currency", out var curr) ? curr.GetString() : "NGN";

        string? accountNumber = null;
        if (data.TryGetProperty("authorization", out var auth) && auth.TryGetProperty("account_number", out var an))
        {
            accountNumber = an.GetString();
        }
        else if (data.TryGetProperty("dedicated_account", out var da) && da.TryGetProperty("account_number", out var daAn))
        {
            accountNumber = daAn.GetString();
        }

        decimal? amount = null;
        if (data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number)
        {
            var rawAmount = am.GetDecimal();
            amount = string.Equals(currency, "NGN", StringComparison.OrdinalIgnoreCase)
                ? rawAmount / 100m
                : rawAmount;
        }

        var outcome = status switch
        {
            "success" => NormalizedWebhookOutcome.Success,
            "failed" => NormalizedWebhookOutcome.Failure,
            "reversed" => NormalizedWebhookOutcome.Reversed,
            "pending" or "processing" => NormalizedWebhookOutcome.Pending,
            _ => eventType.EndsWith(".success", StringComparison.OrdinalIgnoreCase) ? NormalizedWebhookOutcome.Success :
                 eventType.EndsWith(".failed", StringComparison.OrdinalIgnoreCase) ? NormalizedWebhookOutcome.Failure :
                 eventType.EndsWith(".reversed", StringComparison.OrdinalIgnoreCase) ? NormalizedWebhookOutcome.Reversed :
                 NormalizedWebhookOutcome.Unknown
        };

        var providerEventId = !string.IsNullOrWhiteSpace(transferCode)
            ? string.Format(CultureInfo.InvariantCulture, "pstk_evt_{0}_{1}_{2}", eventType, transferCode, status ?? "unknown")
            : !string.IsNullOrWhiteSpace(reference)
                ? string.Format(CultureInfo.InvariantCulture, "pstk_evt_{0}_{1}_{2}", eventType, reference, status ?? "unknown")
                : ComputeHash(rawPayload);

        var safeMeta = JsonSerializer.Serialize(new
        {
            transfer_code = transferCode,
            reference = reference,
            status = status,
            event_type = eventType,
            account_number = accountNumber
        });

        return new NormalizedWebhookEvent(
            Provider: PaymentProvider.Paystack,
            EventType: eventType,
            ProviderEventId: providerEventId,
            InternalReference: reference,
            ProviderReference: transferCode ?? reference,
            AccountNumber: accountNumber,
            Amount: amount,
            Currency: currency,
            Outcome: outcome,
            FailureCode: outcome == NormalizedWebhookOutcome.Failure ? "TRANSFER_FAILED" : null,
            FailureReason: outcome == NormalizedWebhookOutcome.Failure ? string.Format(CultureInfo.InvariantCulture, "Transfer status is '{0}'", status) : null,
            OccurredAtUtc: DateTime.UtcNow,
            SafeMetadata: safeMeta);
    }

    private static NormalizedComplianceWebhookEvent NormalizeDojah(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "verification" : "verification";
        var eventId = root.TryGetProperty("id", out var id) ? id.GetString() : null;

        JsonElement data = root.TryGetProperty("data", out var d) ? d : root;

        var reference = data.TryGetProperty("reference_id", out var refProp) ? refProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(reference))
            reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;

        var status = data.TryGetProperty("status", out var st) ? st.GetString() : null;
        var verificationResult = VerificationResultStatus.ReviewRequired;

        if (status is "success" or "verified" or "MATCH" or "completed")
            verificationResult = VerificationResultStatus.Match;
        else if (status is "failed" or "mismatch" or "rejected" or "NO_MATCH")
            verificationResult = VerificationResultStatus.Mismatch;

        var providerEventId = eventId ?? (!string.IsNullOrWhiteSpace(reference)
            ? $"dojah_evt_{eventType}_{reference}_{status}"
            : $"dojah_evt_{ComputeHash(rawPayload)[..16]}");

        return new NormalizedComplianceWebhookEvent(
            Provider: VerificationProvider.Dojah,
            EventType: eventType,
            ProviderEventId: providerEventId,
            VerificationReference: reference,
            ProviderReference: reference,
            ResultStatus: verificationResult,
            ConfidenceScore: 0.95,
            FailureReason: verificationResult == VerificationResultStatus.Mismatch ? "Dojah reported mismatch" : null,
            SafeMetadata: JsonSerializer.Serialize(new { event_type = eventType, status = status, reference = reference }));
    }

    private static NormalizedComplianceWebhookEvent NormalizeSmileId(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("event_type", out var ev) ? ev.GetString() ?? "job_complete" : "job_complete";
        var jobId = root.TryGetProperty("SmileJobID", out var sj) ? sj.GetString() : null;
        var userId = root.TryGetProperty("UserId", out var u) ? u.GetString() : null;
        var partnerParams = root.TryGetProperty("PartnerParams", out var pp) ? pp : root;

        var jobIdPartner = partnerParams.TryGetProperty("job_id", out var jip) ? jip.GetString() : null;
        var reference = jobIdPartner ?? userId ?? jobId;

        var resultText = root.TryGetProperty("ResultText", out var rt) ? rt.GetString() : null;
        var resultCode = root.TryGetProperty("ResultCode", out var rc) ? rc.GetString() : null;

        var verificationResult = VerificationResultStatus.ReviewRequired;
        if (resultCode == "0810" || resultText?.Contains("Approved", StringComparison.OrdinalIgnoreCase) == true)
            verificationResult = VerificationResultStatus.Match;
        else if (resultCode == "0811" || resultText?.Contains("Rejected", StringComparison.OrdinalIgnoreCase) == true)
            verificationResult = VerificationResultStatus.Mismatch;

        var providerEventId = !string.IsNullOrWhiteSpace(jobId)
            ? $"smile_evt_{jobId}_{resultCode ?? "done"}"
            : $"smile_evt_{ComputeHash(rawPayload)[..16]}";

        return new NormalizedComplianceWebhookEvent(
            Provider: VerificationProvider.SmileId,
            EventType: eventType,
            ProviderEventId: providerEventId,
            VerificationReference: reference,
            ProviderReference: jobId,
            ResultStatus: verificationResult,
            ConfidenceScore: 0.99,
            FailureReason: verificationResult == VerificationResultStatus.Mismatch ? (resultText ?? "Smile ID verification rejected") : null,
            SafeMetadata: JsonSerializer.Serialize(new { job_id = jobId, result_code = resultCode, result_text = resultText }));
    }

    private static NormalizedComplianceWebhookEvent NormalizeNinja(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("type", out var tp) ? tp.GetString() ?? "verification.callback" : "verification.callback";
        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var data = root.TryGetProperty("data", out var d) ? d : root;

        var reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var status = data.TryGetProperty("status", out var st) ? st.GetString() : null;

        var verificationResult = VerificationResultStatus.ReviewRequired;
        if (status is "verified" or "MATCH" or "success")
            verificationResult = VerificationResultStatus.Match;
        else if (status is "failed" or "mismatch" or "rejected")
            verificationResult = VerificationResultStatus.Mismatch;

        var providerEventId = id ?? (!string.IsNullOrWhiteSpace(reference)
            ? $"ninja_evt_{reference}_{status}"
            : $"ninja_evt_{ComputeHash(rawPayload)[..16]}");

        return new NormalizedComplianceWebhookEvent(
            Provider: VerificationProvider.Ninja,
            EventType: eventType,
            ProviderEventId: providerEventId,
            VerificationReference: reference,
            ProviderReference: reference,
            ResultStatus: verificationResult,
            ConfidenceScore: 0.92,
            FailureReason: verificationResult == VerificationResultStatus.Mismatch ? "Ninja reported verification mismatch" : null,
            SafeMetadata: JsonSerializer.Serialize(new { type = eventType, status = status, reference = reference }));
    }

    private static string ComputeHash(string rawPayload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload)));
}
