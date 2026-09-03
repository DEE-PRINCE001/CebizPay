import React, { useState } from 'react';
import Modal from '../common/Modal';
import Badge from '../common/Badge';
import Button from '../common/Button';
import Skeleton from '../common/Skeleton';
import Alert from '../feedback/Alert';
import Table from '../tables/Table';
import TableHeader from '../tables/TableHeader';
import TableRow from '../tables/TableRow';
import { RefreshCw, RotateCcw, XCircle, CheckCircle2, AlertTriangle } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Payroll batch execution progress and line-item retry modal.
 */
export default function PayrollProgressModal({
  isOpen,
  onClose,
  batchId,
  onRefresh
}) {
  const { showSuccess, showError } = useToast();
  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [actionError, setActionError] = useState(null);

  const {
    data: progressData,
    loading,
    error,
    refetch
  } = useApiQuery(
    () => {
      if (!batchId) return Promise.resolve(null);
      return apiClient.get(`/org/payroll/${batchId}`);
    },
    { deps: [batchId], enabled: !!batchId && isOpen }
  );

  const handleRetryFailed = async () => {
    if (!batchId) return;
    setRetrying(true);
    setActionError(null);
    try {
      const res = await apiClient.post(`/org/payroll/${batchId}/retry-failed`);
      showSuccess(res?.message || 'Failed payroll items queued for retry.');
      refetch();
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setActionError(parsed.message || 'Could not retry failed items.');
    } finally {
      setRetrying(false);
    }
  };

  const handleCancelBatch = async () => {
    if (!batchId) return;
    setCancelling(true);
    setActionError(null);
    try {
      await apiClient.post(`/org/payroll/${batchId}/cancel`);
      showSuccess('Payroll batch cancelled.');
      refetch();
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setActionError(parsed.message || 'Could not cancel batch.');
    } finally {
      setCancelling(false);
    }
  };

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'success') {
      return <Badge variant="success" dot={true}>Completed</Badge>;
    }
    if (s === 'processing' || s === 'running') {
      return <Badge variant="brand" dot={true}>Processing</Badge>;
    }
    if (s === 'pending') {
      return <Badge variant="warning" dot={true}>Pending</Badge>;
    }
    if (s === 'failed') {
      return <Badge variant="danger" dot={true}>Failed</Badge>;
    }
    if (s === 'cancelled') {
      return <Badge variant="neutral">Cancelled</Badge>;
    }
    return <Badge variant="neutral">{status || 'Unknown'}</Badge>;
  };

  const items = progressData?.items || progressData?.lineItems || [];
  const processedCount = progressData?.processedCount || progressData?.successfulCount || 0;
  const failedCount = progressData?.failedCount || 0;
  const totalCount = progressData?.totalCount || (processedCount + failedCount) || 1;
  const progressPercent = Math.round((processedCount / totalCount) * 100);

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={`Payroll Batch Details`}
      subtitle={`Reference: ${batchId || '—'}`}
      maxWidth="max-w-2xl"
    >
      <div className="space-y-5 pt-1">
        {actionError && (
          <Alert variant="danger" onClose={() => setActionError(null)}>
            {actionError}
          </Alert>
        )}

        {loading && (
          <div className="space-y-3 p-4">
            <Skeleton variant="card" />
          </div>
        )}

        {!loading && progressData && (
          <>
            {/* Status & Progress Bar */}
            <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-semibold text-slate-700">Execution Status:</span>
                  {getStatusBadge(progressData.status)}
                </div>
                <span className="text-xs font-mono font-bold text-slate-700">{progressPercent}%</span>
              </div>

              {/* Progress Track */}
              <div className="w-full h-2 bg-slate-200 rounded-full overflow-hidden">
                <div
                  className="h-full bg-brand-600 transition-all duration-500 rounded-full"
                  style={{ width: `${progressPercent}%` }}
                />
              </div>

              <div className="grid grid-cols-3 gap-2 pt-1 text-center text-xs">
                <div className="p-2 bg-white rounded-xl border border-slate-100">
                  <span className="text-slate-400 block text-[10px]">Total Staff</span>
                  <span className="font-bold text-slate-900">{totalCount}</span>
                </div>
                <div className="p-2 bg-white rounded-xl border border-slate-100">
                  <span className="text-status-success block text-[10px]">Disbursed</span>
                  <span className="font-bold text-status-success">{processedCount}</span>
                </div>
                <div className="p-2 bg-white rounded-xl border border-slate-100">
                  <span className="text-status-danger block text-[10px]">Failed</span>
                  <span className="font-bold text-status-danger">{failedCount}</span>
                </div>
              </div>
            </div>

            {/* Line Items Table */}
            <div className="space-y-2">
              <span className="text-xs font-bold text-slate-900">Line Items & Vouchers</span>
              <div className="max-h-60 overflow-y-auto border border-slate-200/80 rounded-2xl">
                {items.length > 0 ? (
                  <Table>
                    <TableHeader
                      columns={[
                        { label: 'Employee' },
                        { label: 'Net Amount' },
                        { label: 'Status' }
                      ]}
                    />
                    <tbody>
                      {items.map((item, idx) => (
                        <TableRow key={item.id || idx}>
                          <td className="py-2.5 px-4 text-xs font-medium text-slate-900">
                            {item.employeeName || item.staffName || item.staffId || 'Employee'}
                          </td>
                          <td className="py-2.5 px-4 text-xs font-mono font-bold text-slate-900">
                            ₦{(item.netPay || item.amount || 0).toLocaleString()}
                          </td>
                          <td className="py-2.5 px-4 text-xs">
                            {getStatusBadge(item.status)}
                          </td>
                        </TableRow>
                      ))}
                    </tbody>
                  </Table>
                ) : (
                  <div className="text-center py-6 text-xs text-slate-400">
                    No itemized vouchers available.
                  </div>
                )}
              </div>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-between pt-2 border-t border-slate-100">
              <div className="flex items-center gap-2">
                {failedCount > 0 && (
                  <Button
                    variant="primary"
                    size="sm"
                    icon={RotateCcw}
                    loading={retrying}
                    onClick={handleRetryFailed}
                  >
                    Retry Failed Items
                  </Button>
                )}

                {(progressData.status === 'Pending' || progressData.status === 'Draft') && (
                  <Button
                    variant="danger"
                    size="sm"
                    icon={XCircle}
                    loading={cancelling}
                    onClick={handleCancelBatch}
                  >
                    Cancel Batch
                  </Button>
                )}
              </div>

              <Button
                variant="outline"
                size="sm"
                onClick={onClose}
              >
                Close
              </Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
