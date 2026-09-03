import React, { useState } from 'react';
import ErpLayout from '../../layouts/ErpLayout';
import InventoryTable from '../../components/erp/InventoryTable';
import AddItemModal from '../../components/erp/AddItemModal';
import StockAdjustmentModal from '../../components/erp/StockAdjustmentModal';
import StockMovementsModal from '../../components/erp/StockMovementsModal';
import ValuationPolicyModal from '../../components/erp/ValuationPolicyModal';

import SearchInput from '../../components/forms/SearchInput';
import Select from '../../components/forms/Select';
import TableExport from '../../components/tables/TableExport';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';

import { Package, Plus, Scale, AlertTriangle, Layers, DollarSign, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

const CATEGORIES = [
  { value: '', label: 'All Categories' },
  { value: 'Electronics', label: 'Electronics & Gadgets' },
  { value: 'FMCG', label: 'Fast-Moving Consumer Goods' },
  { value: 'OfficeSupplies', label: 'Office Supplies' },
  { value: 'RawMaterials', label: 'Raw Materials' },
  { value: 'Apparel', label: 'Apparel' },
  { value: 'GeneralGoods', label: 'General Merchandise' }
];

const STOCK_STATUSES = [
  { value: '', label: 'All Stock Levels' },
  { value: 'InStock', label: 'In Stock' },
  { value: 'LowStock', label: 'Low Stock' },
  { value: 'OutOfStock', label: 'Out of Stock' }
];

/**
 * ERP Inventory management and stock valuation workspace.
 */
export default function InventoryPage() {
  const { currentOrgId } = useOrg();

  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [stockStatus, setStockStatus] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 20;

  // Modals state
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [isValuationOpen, setIsValuationOpen] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [adjustingItem, setAdjustingItem] = useState(null);
  const [viewingMovementsItem, setViewingMovementsItem] = useState(null);

  // 1. Fetch Inventory Items
  const {
    data: itemsData,
    loading: itemsLoading,
    error: itemsError,
    refetch: refetchItems
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve({ items: [], totalPages: 1, totalCount: 0 });
      return apiClient.get('/org/inventory/items', {
        params: {
          search: search.trim() || undefined,
          category: category || undefined,
          stockStatus: stockStatus || undefined,
          pageNumber: currentPage,
          pageSize
        }
      });
    },
    { deps: [currentOrgId, search, category, stockStatus, currentPage] }
  );

  // 2. Fetch Valuation Policy
  const {
    data: policyData,
    refetch: refetchPolicy
  } = useApiQuery(
    () => {
      if (!currentOrgId) return Promise.resolve(null);
      return apiClient.get('/org/inventory/valuation-policy').catch(() => null);
    },
    { deps: [currentOrgId] }
  );

  const items = itemsData?.items || [];
  const totalPages = itemsData?.totalPages || 1;
  const totalCount = itemsData?.totalCount || items.length;

  const valuationMethod = policyData?.methodName || policyData?.method || 'Weighted Average (WAC)';
  const totalValuation = items.reduce((acc, item) => acc + (item.quantityOnHand || 0) * (item.averageCost || 0), 0);
  const lowStockCount = items.filter((i) => (i.quantityOnHand || 0) <= (i.reorderLevel || 5)).length;

  const handleRefreshAll = () => {
    refetchItems();
    refetchPolicy();
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <ErpLayout
      title="ERP: Inventory & Stock Valuation"
      subtitle="Stock catalog, continuous valuation policies, and automated reorder tracking"
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
            variant="outline"
            size="sm"
            icon={Scale}
            onClick={() => setIsValuationOpen(true)}
          >
            Valuation Policy
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={() => setIsAddOpen(true)}
          >
            Add Item
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Top Metric Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            icon={DollarSign}
            label="Total Inventory Value"
            value={formatAmount(totalValuation)}
            loading={itemsLoading}
          />
          <StatCard
            icon={Package}
            label="Total Catalog SKUs"
            value={totalCount.toString()}
            loading={itemsLoading}
          />
          <StatCard
            icon={AlertTriangle}
            label="Low Stock Alerts"
            value={lowStockCount.toString()}
          />
          <StatCard
            icon={Scale}
            label="Valuation Method"
            value={valuationMethod}
          />
        </div>

        {/* Search & Filters Toolbar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <SearchInput
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onClear={() => setSearch('')}
            placeholder="Search by SKU or item name..."
            className="w-full sm:max-w-xs"
          />

          <div className="flex flex-wrap items-center gap-2">
            <div className="w-44">
              <Select
                options={CATEGORIES}
                value={category}
                onChange={(e) => {
                  setCategory(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <div className="w-40">
              <Select
                options={STOCK_STATUSES}
                value={stockStatus}
                onChange={(e) => {
                  setStockStatus(e.target.value);
                  setCurrentPage(1);
                }}
              />
            </div>

            <TableExport
              label="Export"
              onExportCsv={() => {
                const csvContent =
                  'data:text/csv;charset=utf-8,' +
                  ['SKU,Name,Category,QtyOnHand,UnitCost,SellingPrice,ReorderLevel']
                    .concat(
                      items.map(
                        (i) =>
                          `"${i.sku || ''}","${i.name || ''}","${i.category || ''}",${i.quantityOnHand || 0},${i.averageCost || 0},${i.sellingPrice || 0},${i.reorderLevel || 0}`
                      )
                    )
                    .join('\n');
                const encodedUri = encodeURI(csvContent);
                const link = document.createElement('a');
                link.setAttribute('href', encodedUri);
                link.setAttribute('download', `inventory_${new Date().toISOString().slice(0, 10)}.csv`);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
              }}
            />
          </div>
        </div>

        {/* Inventory Items Table */}
        <InventoryTable
          items={items}
          loading={itemsLoading}
          error={itemsError}
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={(p) => setCurrentPage(p)}
          onRetry={refetchItems}
          onRefresh={handleRefreshAll}
          onAddItem={() => setIsAddOpen(true)}
          onEditItem={(item) => setEditingItem(item)}
          onAdjustStock={(item) => setAdjustingItem(item)}
          onViewMovements={(item) => setViewingMovementsItem(item)}
        />
      </div>

      {/* Add / Edit Item Modal */}
      <AddItemModal
        isOpen={isAddOpen || !!editingItem}
        onClose={() => {
          setIsAddOpen(false);
          setEditingItem(null);
        }}
        editingItem={editingItem}
        onSuccess={handleRefreshAll}
      />

      {/* Stock Adjustment Modal */}
      <StockAdjustmentModal
        isOpen={!!adjustingItem}
        onClose={() => setAdjustingItem(null)}
        item={adjustingItem}
        onSuccess={handleRefreshAll}
      />

      {/* Stock Movements Ledger Modal */}
      <StockMovementsModal
        isOpen={!!viewingMovementsItem}
        onClose={() => setViewingMovementsItem(null)}
        item={viewingMovementsItem}
      />

      {/* Valuation Policy Configuration Modal */}
      <ValuationPolicyModal
        isOpen={isValuationOpen}
        onClose={() => setIsValuationOpen(false)}
        onChanged={handleRefreshAll}
      />
    </ErpLayout>
  );
}
