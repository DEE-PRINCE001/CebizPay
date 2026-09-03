import React, { useState } from 'react';
import AdminLayout from '../../layouts/AdminLayout';

import Table from '../../components/tables/Table';
import TableHeader from '../../components/tables/TableHeader';
import TableRow from '../../components/tables/TableRow';
import Pagination from '../../components/tables/Pagination';
import Card from '../../components/common/Card';
import Badge from '../../components/common/Badge';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';
import Input from '../../components/forms/Input';
import Alert from '../../components/feedback/Alert';
import Skeleton from '../../components/common/Skeleton';
import EmptyState from '../../components/feedback/EmptyState';
import ErrorState from '../../components/feedback/ErrorState';

import { RefreshCw, Search, CheckCircle2, AlertTriangle, ArrowUpRight } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useToast } from '../../hooks/useToast';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * SuperAdmin financial and compliance reconciliation console.
 */
export default function AdminReconciliationPage() {
  const { showSuccess, showError } = useToast();

  const [requeryReference, setRequeryReference] = useState('');
  const [requeryLoading, setRequeryLoading] = useState(false);
  const [requeryResult, setRequeryResult] = useState(null);
  const [requeryError, setRequeryError] = useState(null);

  const {
    data: recordsData,
    loading,
    error,
    refetch
  } = useApiQuery(() => apiClient.get('/admin/reconciliation/records').catch(() => []));

  const records = Array.isArray(recordsData) ? recordsData : [];

  const handleRequery = async (e) => {
    e.preventDefault();
    if (!requeryReference.trim()) return;

    setRequeryLoading(true);
    setRequeryError(null);
    setRequeryResult(null);

    try {
      const result = await apiClient.post('/admin/reconciliation/requery', {
        reference: requeryReference.trim()
      });
      setRequeryResult(result);
      showSuccess(`Status requery completed for ${requeryReference}.`);
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setRequeryError(parsed.message || 'Requery failed or reference not found on payment rail.');
    } finally {
      setRequeryLoading(false);
    }
  };

  const handleRetryWebhook = async (eventId) => {
    try {
      await apiClient.post(`/admin/reconciliation/events/${eventId}/retry`);
      showSuccess('Webhook event redelivered.');
      refetch();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to retry webhook event.');
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return '—';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  };

  return (
    <AdminLayout
      title="Settlement & Reconciliation Engine"
      subtitle="Multi-rail ledger settlement, provider status requeries, and dead-letter retry"
      headerAction={
        <Button
          variant="outline"
          size="sm"
          icon={RefreshCw}
          onClick={refetch}
        >
          Refresh Records
        </Button>
      }
    >
      <div className="space-y-6">
        {/* On-Demand Requery Card */}
        <Card padding="p-6" className="bg-white border border-slate-200/80">
          <h4 className="text-xs font-bold text-slate-900 mb-1">On-Demand Provider Status Requery</h4>
          <p className="text-xs text-slate-500 mb-4">
            Directly poll upstream banking switches (NIBSS, Interswitch, Providus, Flutterwave) to synchronize ledger state.
          </p>

          <form onSubmit={handleRequery} className="flex flex-col sm:flex-row gap-3">
            <div className="flex-1">
              <Input
                placeholder="Enter Transaction Reference (e.g. TX-984920482)..."
                value={requeryReference}
                onChange={(e) => {
                  setRequeryReference(e.target.value);
                  if (requeryError) setRequeryError(null);
                }}
                required
              />
            </div>
            <Button
              type="submit"
              variant="primary"
              size="md"
              loading={requeryLoading}
              icon={Search}
            >
              Requery Payment Rail
            </Button>
          </form>

          {requeryError && (
            <Alert variant="danger" onClose={() => setRequeryError(null)} className="mt-4">
              {requeryError}
            </Alert>
          )}

          {requeryResult && (
            <div className="mt-4 p-4 bg-brand-50/60 border border-brand-200 rounded-2xl text-xs space-y-1">
              <div className="flex items-center gap-2 text-brand-900 font-bold">
                <CheckCircle2 size={16} className="text-brand-600" />
                <span>Requery Completed Successfully</span>
              </div>
              <div className="text-slate-600">
                Provider Status: <strong className="text-slate-900">{requeryResult.status || 'Settled'}</strong> • Amount: ₦{(requeryResult.amount || 0).toLocaleString()}
              </div>
            </div>
          )}
        </Card>

        {/* Reconciliation Records Table */}
        <Card padding="p-0" className="overflow-hidden">
          <div className="p-4 border-b border-slate-100 flex items-center justify-between">
            <h4 className="text-xs font-bold text-slate-900">Unsettled / Discrepancy Records</h4>
            <span className="text-xs text-slate-400">{records.length} records</span>
          </div>

          {loading && (
            <div className="p-6 space-y-3">
              <Skeleton variant="table-row" count={5} />
            </div>
          )}

          {!loading && error && (
            <div className="p-6">
              <ErrorState
                title="Failed to load reconciliation records"
                message={error.message || 'Unable to retrieve reconciliation data.'}
                onRetry={refetch}
              />
            </div>
          )}

          {!loading && !error && records.length === 0 && (
            <div className="p-8">
              <EmptyState
                icon={CheckCircle2}
                title="All accounts reconciled"
                description="Zero discrepancies detected across upstream payment switches and central ledger."
              />
            </div>
          )}

          {!loading && !error && records.length > 0 && (
            <Table>
              <TableHeader
                columns={[
                  { label: 'Reference' },
                  { label: 'Payment Rail' },
                  { label: 'Ledger State' },
                  { label: 'Provider State' },
                  { label: 'Discrepancy Amount' },
                  { label: 'Timestamp' }
                ]}
              />
              <tbody>
                {records.map((r, idx) => (
                  <TableRow key={r.id || idx}>
                    <td className="py-3 px-4 text-xs font-bold text-slate-900 font-mono">
                      {r.reference || 'REF-N/A'}
                    </td>
                    <td className="py-3 px-4 text-xs font-semibold text-slate-700">
                      {r.provider || 'NIBSS'}
                    </td>
                    <td className="py-3 px-4 text-xs text-slate-600">
                      {r.ledgerStatus || 'Pending'}
                    </td>
                    <td className="py-3 px-4 text-xs text-slate-600">
                      {r.providerStatus || 'Unknown'}
                    </td>
                    <td className="py-3 px-4 text-xs font-mono font-bold text-slate-900">
                      ₦{(r.amount || 0).toLocaleString()}
                    </td>
                    <td className="py-3 px-4 text-xs text-slate-400">
                      {formatDate(r.timestampUtc || r.createdAtUtc)}
                    </td>
                  </TableRow>
                ))}
              </tbody>
            </Table>
          )}
        </Card>
      </div>
    </AdminLayout>
  );
}
