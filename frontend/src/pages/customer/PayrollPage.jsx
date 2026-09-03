import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import PayrollBatchList from '../../components/payroll/PayrollBatchList';
import RunPayrollWizardModal from '../../components/payroll/RunPayrollWizardModal';
import PayrollProgressModal from '../../components/payroll/PayrollProgressModal';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';
import { Users, Receipt, Calendar, Plus, RefreshCw, Zap } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

/**
 * Corporate payroll operations and execution workspace.
 */
export default function PayrollPage() {
  const { currentOrgId } = useOrg();

  const [currentPage, setCurrentPage] = useState(1);
  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const [selectedBatchId, setSelectedBatchId] = useState(null);

  // 1. Fetch Staff Count
  const {
    data: staffData,
    loading: staffLoading,
    refetch: refetchStaff
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalCount: 0 });
      return apiClient.get('/org/staff', { params: { pageSize: 1 } });
    },
    { deps: [currentOrgId] }
  );

  // 2. Fetch Payroll Batch Settlements
  const {
    data: batchData,
    loading: batchesLoading,
    error: batchesError,
    refetch: refetchBatches
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/reports/settlements', {
        params: {
          pageNumber: currentPage,
          pageSize: 15,
          settlementMethod: 'Payroll'
        }
      }).catch(() => ({ items: [], totalPages: 1, totalCount: 0 }));
    },
    { deps: [currentOrgId, currentPage] }
  );

  const batches = batchData?.items || batchData?.records || [];
  const totalPages = batchData?.totalPages || 1;
  const staffCount = staffData?.totalCount || 0;

  const handleRefreshAll = () => {
    refetchStaff();
    refetchBatches();
  };

  return (
    <CustomerLayout
      title="Corporate Payroll"
      subtitle="Automated batch calculations, workforce salary disbursals, and payment vouchers"
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
            icon={Zap}
            onClick={() => setIsWizardOpen(true)}
          >
            Run Payroll
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={Users}
            label="Active Employees"
            value={staffCount.toString()}
            loading={staffLoading}
          />
          <StatCard
            icon={Receipt}
            label="Total Payroll Runs"
            value={batches.length.toString()}
            loading={batchesLoading}
          />
          <StatCard
            icon={Calendar}
            label="Current Cycle"
            value={new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(new Date())}
          />
        </div>

        {/* Batches Table */}
        <PayrollBatchList
          batches={batches}
          loading={batchesLoading}
          error={batchesError}
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={(p) => setCurrentPage(p)}
          onRetry={refetchBatches}
          onViewBatch={(id) => setSelectedBatchId(id)}
          onRunPayroll={() => setIsWizardOpen(true)}
        />
      </div>

      {/* Execution Wizard */}
      <RunPayrollWizardModal
        isOpen={isWizardOpen}
        onClose={() => setIsWizardOpen(false)}
        onSuccess={handleRefreshAll}
      />

      {/* Batch Progress & Line Items Modal */}
      <PayrollProgressModal
        isOpen={!!selectedBatchId}
        onClose={() => setSelectedBatchId(null)}
        batchId={selectedBatchId}
        onRefresh={handleRefreshAll}
      />
    </CustomerLayout>
  );
}
