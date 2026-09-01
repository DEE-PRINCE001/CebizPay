#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class CompliancePiiSanitizationTests
{
    [Fact]
    public void VerificationEvidence_Create_PreservesSafeMetadataWithoutSecrets()
    {
        var evidence = VerificationEvidence.Create(
            verificationOperationId: Guid.NewGuid(),
            verificationType: VerificationType.IndividualKyc,
            capability: VerificationCapability.Identity,
            provider: VerificationProvider.Dojah,
            resultStatus: VerificationResultStatus.Match,
            userId: "usr_12345",
            providerReference: "dojah_txn_777",
            confidenceScore: 98.5m,
            safeMetadata: "{\"verifiedFields\":[\"FirstName\",\"LastName\",\"DateOfBirth\"],\"matchConfidence\":98.5}");

        Assert.NotNull(evidence.SafeMetadata);
        Assert.DoesNotContain("sk_", evidence.SafeMetadata);
        Assert.DoesNotContain("secret", evidence.SafeMetadata, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verifiedFields", evidence.SafeMetadata);
    }

    [Fact]
    public void VerificationOperation_GenerateReference_FormatsProperPrefixes()
    {
        var kycOp = VerificationOperation.Create(
            VerificationOperation.GenerateReference(VerificationType.IndividualKyc),
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            VerificationProvider.Dojah,
            userId: "usr_123");

        var kybOp = VerificationOperation.Create(
            VerificationOperation.GenerateReference(VerificationType.OrganizationKyb),
            VerificationType.OrganizationKyb,
            VerificationCapability.Business,
            VerificationProvider.Dojah,
            organizationId: Guid.NewGuid());

        Assert.StartsWith("CBZKYC-", kycOp.Reference, StringComparison.Ordinal);
        Assert.StartsWith("CBZKYB-", kybOp.Reference, StringComparison.Ordinal);
    }
}
