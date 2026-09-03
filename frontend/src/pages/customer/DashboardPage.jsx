import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import BalanceCard from '../../components/dashboard/BalanceCard';
import MetricGrid from '../../components/dashboard/MetricGrid';
import QuickActions from '../../components/dashboard/QuickActions';
import RecentTransactions from '../../components/dashboard/RecentTransactions';
import QuickTransferModal from '../../components/dashboard/QuickTransferModal';
import FundWalletModal from '../../components/dashboard/FundWalletModal';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import { useAuth } from '../../context/AuthContext';
import apiClient from '../../services/api/client';
import Button from '../../components/common/Button';
import { Plus, RefreshCw } from 'lucide-react';

/**
 * Customer & organization dashboard viewport.
 */
export default function DashboardPage() {
  const { currentOrg, currentOrgId } = useOrg();
  const { user } = useAuth();

  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isFundOpen, setIsFundOpen] = useState(false);

  // 1. Fetch Primary Dedicated Virtual Account for auto-funding
  const {
    data: virtualAccount,
    loading: vaLoading,
    refetch: refetchVa
  } = useApiQuery(
    () => apiClient.get('/virtual-accounts/primary', { params: { currency: 'NGN' } }).catch(() => null),
    { deps: [currentOrgId] }
  );

  // 2. Fetch External Funding Accounts & Balances
  const {
    data: externalAccounts,
    loading: acctLoading,
    refetch: refetchAccts
  } = useApiQuery(
    () => apiClient.get('/wallet/external-accounts', { params: { organizationId: currentOrgId } }).catch(() => []),
    { deps: [currentOrgId] }
  );

  // 3. Fetch Recent Settlements & Ledger Transactions
  const {
    data: settlementData,
    loading: txLoading,
    error: txError,
    refetch: refetchTxs
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [] });
      return apiClient.get('/org/reports/settlements', { params: { pageSize: 10 } }).catch(() => ({ items: [] }));
    },
    { deps: [currentOrgId] }
  );

  const primaryAcct = Array.isArray(externalAccounts) ? externalAccounts[0] : null;
  const balance = primaryAcct?.currentBalance || primaryAcct?.balance || 0;

  const transactionsList = settlementData?.items || settlementData?.records || [];

  const metrics = {
    organisationsCount: currentOrg ? 1 : 0,
    individualsCount: 1,
    pendingUsersCount: 0,
    activeUsersCount: 1,
    rejectedUsersCount: 0,
    savingPlansCount: 0
  };

  const handleRefreshAll = () => {
    refetchVa();
    refetchAccts();
    refetchTxs();
  };

  return (
    <CustomerLayout
      title={`Welcome back, ${user?.firstName || user?.fullName || 'User'}`}
      subtitle={`Overview of ${currentOrg?.name || 'your corporate wallet'} and workforce activities`}
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={handleRefreshAll}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={() => setIsFundOpen(true)}
          >
            Fund Wallet
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Top Hero: Wallet Balance Card + Quick Actions */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2">
            <BalanceCard
              balance={balance}
              currency="NGN"
              virtualAccount={virtualAccount}
              loading={vaLoading || acctLoading}
              onFundWallet={() => setIsFundOpen(true)}
              onTransfer={() => setIsTransferOpen(true)}
            />
          </div>
          <div className="flex flex-col justify-between">
            <QuickActions
              onFundWallet={() => setIsFundOpen(true)}
              onTransfer={() => setIsTransferOpen(true)}
              className="h-full justify-between"
            />
          </div>
        </div>

        {/* 6-Stat Metric Widgets */}
        <section className="space-y-2">
          <MetricGrid metrics={metrics} loading={false} />
        </section>

        {/* Recent Transactions Ledger Table */}
        <section className="space-y-2">
          <RecentTransactions
            transactions={transactionsList}
            loading={txLoading}
            error={txError}
            onRetry={refetchTxs}
          />
        </section>
      </div>

      {/* Modals */}
      <QuickTransferModal
        isOpen={isTransferOpen}
        onClose={() => setIsTransferOpen(false)}
        onSuccess={() => {
          setIsTransferOpen(false);
          handleRefreshAll();
        }}
      />

      <FundWalletModal
        isOpen={isFundOpen}
        onClose={() => setIsFundOpen(false)}
        virtualAccount={virtualAccount}
      />
    </CustomerLayout>
  );
}
