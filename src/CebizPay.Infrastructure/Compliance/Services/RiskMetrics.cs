#pragma warning disable CS1591
using System.Diagnostics.Metrics;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Services;

/// <summary>
/// OpenTelemetry metrics publisher for Risk Engine, CDD, EDD, and Compliance Decisioning.
/// Emits strictly sanitized labels only (no PII, no account numbers, no names).
/// </summary>
public sealed class RiskMetrics
{
    public const string MeterName = "CebizPay.Risk";

    private readonly Counter<long> _riskAssessmentsCounter;
    private readonly Counter<long> _riskReassessmentsCounter;
    private readonly Counter<long> _cddCompletedCounter;
    private readonly Counter<long> _eddRequiredCounter;
    private readonly Counter<long> _eddCompletedCounter;
    private readonly Counter<long> _complianceReviewsCounter;
    private readonly Counter<long> _complianceRestrictionsCounter;
    private readonly Counter<long> _transactionEligibilityRejectionsCounter;

    public RiskMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _riskAssessmentsCounter = meter.CreateCounter<long>(
            "cebizpay.risk.assessments_total",
            description: "Total number of compliance risk assessments completed.");

        _riskReassessmentsCounter = meter.CreateCounter<long>(
            "cebizpay.risk.reassessments_total",
            description: "Total number of risk reassessments resulting in rating changes.");

        _cddCompletedCounter = meter.CreateCounter<long>(
            "cebizpay.risk.cdd_completed_total",
            description: "Total number of Customer Due Diligence (CDD) profiles completed.");

        _eddRequiredCounter = meter.CreateCounter<long>(
            "cebizpay.risk.edd_required_total",
            description: "Total number of Enhanced Due Diligence (EDD) requirements triggered.");

        _eddCompletedCounter = meter.CreateCounter<long>(
            "cebizpay.risk.edd_completed_total",
            description: "Total number of Enhanced Due Diligence (EDD) cases decided.");

        _complianceReviewsCounter = meter.CreateCounter<long>(
            "cebizpay.risk.compliance_reviews_total",
            description: "Total number of manual compliance reviews initiated.");

        _complianceRestrictionsCounter = meter.CreateCounter<long>(
            "cebizpay.risk.restrictions_placed_total",
            description: "Total number of compliance restrictions placed.");

        _transactionEligibilityRejectionsCounter = meter.CreateCounter<long>(
            "cebizpay.risk.transaction_compliance_rejections_total",
            description: "Total number of transactions blocked by compliance gating.");
    }

    public void RecordRiskAssessment(RiskSubjectType subjectType, RiskRating riskRating)
    {
        _riskAssessmentsCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()),
            new KeyValuePair<string, object?>("risk_rating", riskRating.ToString()));
    }

    public void RecordRiskChanged(RiskSubjectType subjectType, RiskRating previousRating, RiskRating newRating)
    {
        _riskReassessmentsCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()),
            new KeyValuePair<string, object?>("previous_rating", previousRating.ToString()),
            new KeyValuePair<string, object?>("new_rating", newRating.ToString()));
    }

    public void RecordCddCompleted(RiskSubjectType subjectType, CddLevel level)
    {
        _cddCompletedCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()),
            new KeyValuePair<string, object?>("cdd_level", level.ToString()));
    }

    public void RecordEddRequired(RiskSubjectType subjectType)
    {
        _eddRequiredCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()));
    }

    public void RecordEddCompleted(RiskSubjectType subjectType, ComplianceDecisionType decision)
    {
        _eddCompletedCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()),
            new KeyValuePair<string, object?>("decision", decision.ToString()));
    }

    public void RecordComplianceReview(RiskSubjectType subjectType)
    {
        _complianceReviewsCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()));
    }

    public void RecordRestrictionPlaced(RiskSubjectType subjectType, ComplianceRestrictionType restrictionType)
    {
        _complianceRestrictionsCounter.Add(1,
            new KeyValuePair<string, object?>("subject_type", subjectType.ToString()),
            new KeyValuePair<string, object?>("restriction_type", restrictionType.ToString()));
    }

    public void RecordEligibilityRejection(ComplianceOperationType operationType, EligibilityStatus status)
    {
        _transactionEligibilityRejectionsCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operationType.ToString()),
            new KeyValuePair<string, object?>("status", status.ToString()));
    }
}
