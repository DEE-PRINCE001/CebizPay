import React, { useState } from 'react';
import Modal from '../common/Modal';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import Badge from '../common/Badge';
import { ShieldAlert, CheckCircle2, XCircle, FileQuestion, UserCheck } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to review, request documentation, or decide on Enhanced Due Diligence cases.
 */
export default function EddCaseReviewModal({
  isOpen,
  onClose,
  eddCase,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [decisionMode, setDecisionMode] = useState(null); // 'approve' | 'reject' | 'request-info'
  const [reason, setReason] = useState('');
  const [seniorSignoff, setSeniorSignoff] = useState(true);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  if (!eddCase) return null;

  const handleExecuteAction = async (e) => {
    e.preventDefault();

    if (!reason.trim()) {
      setError('Please provide detailed compliance justification or document requirements.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (decisionMode === 'approve') {
        await apiClient.post(`/admin/compliance/edd/cases/${eddCase.id}/approve`, {
          reason: reason.trim(),
          isSeniorManagement: seniorSignoff
        });
        showSuccess('EDD case approved.');
      } else if (decisionMode === 'reject') {
        await apiClient.post(`/admin/compliance/edd/cases/${eddCase.id}/reject`, {
          reason: reason.trim()
        });
        showSuccess('EDD case rejected and restrictions maintained.');
      } else if (decisionMode === 'request-info') {
        await apiClient.post(`/admin/compliance/edd/cases/${eddCase.id}/request-information`, {
          additionalRequirement: reason.trim()
        });
        showSuccess('Additional compliance documentation requested from customer.');
      }

      setReason('');
      setDecisionMode(null);
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to submit EDD decision.');
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={() => {
        setDecisionMode(null);
        onClose();
      }}
      title="Enhanced Due Diligence (EDD) Case"
      subtitle={`Case ID: ${eddCase.id?.slice(0, 8)}... • Subject: ${eddCase.subjectId || 'User'}`}
      maxWidth="max-w-lg"
    >
      <div className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {/* Case Metadata */}
        <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-2.5 text-xs">
          <div className="flex items-center justify-between">
            <span className="text-slate-500">Subject Type</span>
            <span className="font-bold text-slate-900">{eddCase.subjectType || 'Individual'}</span>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-slate-500">Case Trigger Date</span>
            <span className="font-bold text-slate-800">{formatDate(eddCase.createdAtUtc)}</span>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-slate-500">Case Status</span>
            <Badge variant={eddCase.status === 'Approved' ? 'success' : 'warning'}>
              {eddCase.status || 'PendingReview'}
            </Badge>
          </div>

          {eddCase.triggerReason && (
            <div className="pt-2 border-t border-slate-200 text-slate-700">
              <span className="font-semibold block mb-0.5 text-slate-900">Trigger Reason:</span>
              <p className="text-[11px] leading-relaxed bg-white p-2.5 rounded-xl border border-slate-200/60">
                {eddCase.triggerReason}
              </p>
            </div>
          )}
        </div>

        {/* Action Form */}
        {decisionMode ? (
          <form onSubmit={handleExecuteAction} className="space-y-3.5 pt-1">
            <Textarea
              label={
                decisionMode === 'request-info'
                  ? 'Information / Documents to Request'
                  : 'Mandatory Compliance Justification'
              }
              rows={3}
              placeholder="Detailed justification or list of required documents..."
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (error) setError(null);
              }}
              required
            />

            {decisionMode === 'approve' && (
              <label className="flex items-center gap-2 cursor-pointer select-none text-xs text-slate-700">
                <input
                  type="checkbox"
                  checked={seniorSignoff}
                  onChange={(e) => setSeniorSignoff(e.target.checked)}
                  className="w-4 h-4 text-brand-600 rounded-md border-slate-300 focus:ring-brand-500"
                />
                <span className="font-semibold">Executive Senior Management Sign-off</span>
              </label>
            )}

            <div className="flex items-center gap-2 pt-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setDecisionMode(null)}
                disabled={loading}
                className="flex-1"
              >
                Back
              </Button>
              <Button
                type="submit"
                variant={decisionMode === 'reject' ? 'danger' : 'primary'}
                size="sm"
                loading={loading}
                icon={decisionMode === 'reject' ? XCircle : CheckCircle2}
                className="flex-1"
              >
                Confirm {decisionMode === 'request-info' ? 'Request' : decisionMode === 'approve' ? 'Approval' : 'Rejection'}
              </Button>
            </div>
          </form>
        ) : (
          <div className="flex items-center gap-2 pt-2 border-t border-slate-100">
            <Button
              variant="outline"
              size="sm"
              icon={FileQuestion}
              onClick={() => setDecisionMode('request-info')}
              className="flex-1"
            >
              Request Info
            </Button>
            <Button
              variant="outline"
              size="sm"
              icon={XCircle}
              onClick={() => setDecisionMode('reject')}
              className="flex-1 text-status-danger hover:bg-status-danger-bg"
            >
              Reject Case
            </Button>
            <Button
              variant="primary"
              size="sm"
              icon={CheckCircle2}
              onClick={() => setDecisionMode('approve')}
              className="flex-1"
            >
              Approve EDD
            </Button>
          </div>
        )}
      </div>
    </Modal>
  );
}
