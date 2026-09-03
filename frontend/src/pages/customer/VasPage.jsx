import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import Tabs from '../../components/common/Tabs';
import AirtimeForm from '../../components/vas/AirtimeForm';
import DataBundleForm from '../../components/vas/DataBundleForm';
import ElectricityForm from '../../components/vas/ElectricityForm';
import CableTvForm from '../../components/vas/CableTvForm';
import VasTransactionsTable from '../../components/vas/VasTransactionsTable';
import TransactionReceiptDrawer from '../../components/wallet/TransactionReceiptDrawer';
import { Smartphone, Wifi, Zap, Tv, History } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

/**
 * Main VAS & utility bill payment workspace.
 */
export default function VasPage() {
  const { currentOrgId } = useOrg();
  const [activeTab, setActiveTab] = useState('airtime');
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedTransaction, setSelectedTransaction] = useState(null);

  const vasTabs = [
    { id: 'airtime', label: 'Airtime Top-up', icon: Smartphone },
    { id: 'data', label: 'Data Bundles', icon: Wifi },
    { id: 'electricity', label: 'Electricity Bills', icon: Zap },
    { id: 'cable', label: 'Cable TV', icon: Tv },
    { id: 'history', label: 'Purchase History', icon: History }
  ];

  const {
    data: settlementData,
    loading: txLoading,
    error: txError,
    refetch: refetchTxs
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient
        .get('/org/reports/settlements', {
          params: {
            pageNumber: currentPage,
            pageSize: 15,
            settlementMethod: 'VAS'
          }
        })
        .catch(() => ({ items: [], totalPages: 1, totalCount: 0 }));
    },
    { deps: [currentOrgId, currentPage] }
  );

  const transactionsList = settlementData?.items || settlementData?.records || [];
  const totalPages = settlementData?.totalPages || 1;

  const handlePurchaseSuccess = () => {
    refetchTxs();
  };

  return (
    <CustomerLayout
      title="VAS & Utility Recharge"
      subtitle="Instant airtime, mobile internet bundles, electricity tokens, and TV subscriptions"
    >
      <div className="space-y-6">
        {/* Navigation Tabs */}
        <div className="border-b border-slate-200/80">
          <Tabs
            variant="underlined"
            tabs={vasTabs}
            activeTab={activeTab}
            onChange={(tab) => setActiveTab(tab)}
          />
        </div>

        {/* Tab Viewports */}
        <div className="pt-2">
          {activeTab === 'airtime' && (
            <AirtimeForm onSuccess={handlePurchaseSuccess} />
          )}

          {activeTab === 'data' && (
            <DataBundleForm onSuccess={handlePurchaseSuccess} />
          )}

          {activeTab === 'electricity' && (
            <ElectricityForm onSuccess={handlePurchaseSuccess} />
          )}

          {activeTab === 'cable' && (
            <CableTvForm onSuccess={handlePurchaseSuccess} />
          )}

          {activeTab === 'history' && (
            <VasTransactionsTable
              transactions={transactionsList}
              loading={txLoading}
              error={txError}
              currentPage={currentPage}
              totalPages={totalPages}
              onPageChange={(p) => setCurrentPage(p)}
              onRetry={refetchTxs}
              onViewDetails={(tx) => setSelectedTransaction(tx)}
            />
          )}
        </div>
      </div>

      <TransactionReceiptDrawer
        isOpen={!!selectedTransaction}
        onClose={() => setSelectedTransaction(null)}
        transaction={selectedTransaction}
      />
    </CustomerLayout>
  );
}
