import React, { useState } from 'react';
import Card from '../common/Card';
import Button from '../common/Button';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ConfirmModal from '../feedback/ConfirmModal';
import { Building, Plus, CheckCircle2, Trash2, Star, RefreshCw } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Linked funding accounts and dedicated virtual accounts manager.
 */
export default function ExternalAccountsList({
  accounts = [],
  loading = false,
  onRefresh,
  organizationId = null,
  className = ''
}) {
  const { showSuccess, showError } = useToast();
  const [actionLoadingId, setActionLoadingId] = useState(null);
  const [provisioning, setProvisioning] = useState(false);
  const [accountToDelete, setAccountToDelete] = useState(null);

  const handleSetPrimary = async (accountId) => {
    setActionLoadingId(accountId);
    try {
      await apiClient.post(`/wallet/external-accounts/${accountId}/primary`, null, {
        params: { organizationId }
      });
      showSuccess('Primary funding account updated.');
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to set primary account.');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleDeactivate = async () => {
    if (!accountToDelete) return;
    setActionLoadingId(accountToDelete.id);
    try {
      await apiClient.delete(`/wallet/external-accounts/${accountToDelete.id}`, {
        params: { organizationId }
      });
      showSuccess('Funding account deactivated.');
      setAccountToDelete(null);
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to deactivate account.');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleProvisionMonnify = async () => {
    setProvisioning(true);
    try {
      await apiClient.post('/wallet/external-accounts/monnify', null, {
        params: { organizationId, currency: 'NGN' }
      });
      showSuccess('New Monnify reserved account provisioned.');
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Could not provision new virtual account.');
    } finally {
      setProvisioning(false);
    }
  };

  return (
    <div className={`space-y-4 ${className}`}>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-bold text-slate-900">Linked Funding & Virtual Accounts</h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Dedicated payment accounts linked to this wallet for automated ledger funding.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={onRefresh}
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            loading={provisioning}
            onClick={handleProvisionMonnify}
          >
            Provision New Account
          </Button>
        </div>
      </div>

      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Skeleton variant="card" count={2} />
        </div>
      )}

      {!loading && accounts.length === 0 && (
        <Card padding="p-8">
          <EmptyState
            icon={Building}
            title="No linked funding accounts"
            description="Provision a dedicated virtual account to enable automatic bank transfer funding for your corporate wallet."
            actionLabel="Provision Monnify Account"
            onAction={handleProvisionMonnify}
          />
        </Card>
      )}

      {!loading && accounts.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {accounts.map((acct) => {
            const isPrimary = acct.isPrimary || acct.primary;
            const isLoading = actionLoadingId === acct.id;

            return (
              <Card key={acct.id} padding="p-5" className="relative flex flex-col justify-between space-y-4">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-xl bg-slate-100 flex items-center justify-center text-slate-600">
                      <Building size={20} />
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <h4 className="text-sm font-bold text-slate-900">{acct.bankName || 'Reserved Bank Account'}</h4>
                        {isPrimary && (
                          <Badge variant="brand" size="sm">
                            Primary
                          </Badge>
                        )}
                      </div>
                      <p className="text-xs text-slate-500 font-mono mt-0.5">{acct.accountNumber}</p>
                    </div>
                  </div>

                  <Badge variant={acct.status === 'Active' || !acct.status ? 'success' : 'neutral'} size="sm" dot={true}>
                    {acct.status || 'Active'}
                  </Badge>
                </div>

                <div className="pt-2 border-t border-slate-100 flex items-center justify-between text-xs">
                  <span className="text-slate-500">
                    Currency: <strong className="text-slate-700">{acct.currency || 'NGN'}</strong>
                  </span>

                  <div className="flex items-center gap-2">
                    {!isPrimary && (
                      <button
                        type="button"
                        disabled={isLoading}
                        onClick={() => handleSetPrimary(acct.id)}
                        className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700 hover:underline disabled:opacity-50"
                      >
                        <Star size={13} />
                        <span>Set Primary</span>
                      </button>
                    )}

                    <button
                      type="button"
                      disabled={isLoading}
                      onClick={() => setAccountToDelete(acct)}
                      className="p-1.5 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 transition-colors disabled:opacity-50"
                      aria-label="Deactivate Account"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}

      {/* Confirmation Modal */}
      {accountToDelete && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setAccountToDelete(null)}
          onConfirm={handleDeactivate}
          title="Deactivate Funding Account"
          message={`Are you sure you want to deactivate ${accountToDelete.bankName} (${accountToDelete.accountNumber})? You will no longer receive automated wallet deposits through this account.`}
          confirmText="Deactivate"
          confirmVariant="danger"
          loading={actionLoadingId === accountToDelete.id}
        />
      )}
    </div>
  );
}
