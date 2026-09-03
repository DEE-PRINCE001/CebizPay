import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import ServicesTable from '../../components/erp/ServicesTable';
import AddServiceModal from '../../components/erp/AddServiceModal';

import SearchInput from '../../components/forms/SearchInput';
import TableExport from '../../components/tables/TableExport';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';

import { Briefcase, Plus, DollarSign, Layers, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

/**
 * ERP Services catalog and rate card management workspace.
 */
export default function ServicesPage() {
  const { currentOrgId } = useOrg();

  const [search, setSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  // Modals state
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editingService, setEditingService] = useState(null);

  // 1. Fetch Services Catalog
  const {
    data: servicesData,
    loading: servicesLoading,
    error: servicesError,
    refetch: refetchServices
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/services', {
        params: {
          search: search.trim() || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, currentPage] }
  );

  const services = servicesData?.items || [];
  const totalPages = servicesData?.totalPages || 1;
  const totalCount = servicesData?.totalCount || services.length;

  const activeServicesCount = services.filter((s) => s.status !== 'Inactive').length;
  const avgRate = services.length > 0
    ? services.reduce((acc, s) => acc + (s.unitPrice || 0), 0) / services.length
    : 0;

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <ErpLayout
      title="ERP: Service Catalog"
      subtitle="Billable service offerings, hourly rate cards, and consulting fees"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={refetchServices}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={() => setIsAddOpen(true)}
          >
            Add Service
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Top Metric Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={Briefcase}
            label="Total Services"
            value={totalCount.toString()}
            loading={servicesLoading}
          />
          <StatCard
            icon={Layers}
            label="Active Offerings"
            value={activeServicesCount.toString()}
          />
          <StatCard
            icon={DollarSign}
            label="Average Billing Rate"
            value={formatAmount(avgRate)}
          />
        </div>

        {/* Search & Export Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by code or service name..."
            className="w-full sm:max-w-xs"
          />

          <TableExport
            label="Export"
            onExportCsv={() => {
              const csvContent =
                'data:text/csv;charset=utf-8,' +
                ['Code,Name,UnitPrice,Status']
                  .concat(
                    services.map(
                      (s) =>
                        `"${s.code || ''}","${s.name || ''}",${s.unitPrice || 0},"${s.status || 'Active'}"`
                    )
                  )
                  .join('\n');
              const encodedUri = encodeURI(csvContent);
              const link = document.createElement('a');
              link.setAttribute('href', encodedUri);
              link.setAttribute('download', `services_${new Date().toISOString().slice(0, 10)}.csv`);
              document.body.appendChild(link);
              link.click();
              document.body.removeChild(link);
            }}
          />
        </div>

        {/* Services Table */}
        <ServicesTable
          services={services}
          loading={servicesLoading}
          error={servicesError}
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={(p) => setCurrentPage(p)}
          onRetry={refetchServices}
          onRefresh={refetchServices}
          onAddService={() => setIsAddOpen(true)}
          onEditService={(svc) => setEditingService(svc)}
        />
      </div>

      {/* Add / Edit Service Modal */}
      <AddServiceModal
        isOpen={isAddOpen || !!editingService}
        onClose={() => {
          setIsAddOpen(false);
          setEditingService(null);
        }}
        editingService={editingService}
        onSuccess={refetchServices}
      />
    </ErpLayout>
  );
}
