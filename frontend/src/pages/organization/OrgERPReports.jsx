import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { LineChart, DollarSign, ArrowUpRight, ArrowDownLeft, FileSpreadsheet, Printer } from 'lucide-react';

export default function OrgERPReports() {
  const [activeTab, setActiveTab] = useState('pnl'); // 'pnl' | 'sales' | 'purchases' | 'settlements'

  // Profit & Loss statement figures
  const pnl = {
    grossRevenue: 34500000.0,
    costOfGoodsServices: 11400000.0,
    grossProfit: 23100000.0,
    operatingExpenses: 5800000.0,
    payrollExpenses: 11840000.0,
    ebitda: 5460000.0,
    taxEstimate: 409500.0,
    netProfit: 5050500.0,
    marginPct: '14.64%'
  };

  const salesTransactions = [
    { id: 'TXN-SLS-01', customer: 'FirstBank Digital Innovations', description: 'Core Banking API SOW 1', gross: 5375000.0, vat: 375000.0, netRevenue: 5000000.0, date: '2026-08-25T11:00:00Z' },
    { id: 'TXN-SLS-02', customer: 'Moniepoint MFB Corporate', description: 'Settlement Rail Setup', gross: 3762500.0, vat: 262500.0, netRevenue: 3500000.0, date: '2026-08-20T14:30:00Z' }
  ];

  const salesColumns = [
    { header: 'Sale Reference', accessor: 'id', render: (row) => <span className="font-mono font-bold text-slate-900">{row.id}</span> },
    { header: 'Customer', accessor: 'customer', render: (row) => <span className="font-bold text-slate-800 text-xs">{row.customer}</span> },
    { header: 'Description', accessor: 'description', render: (row) => <span className="text-slate-600 text-xs">{row.description}</span> },
    { header: 'Net Revenue (Excl. VAT)', accessor: 'netRevenue', render: (row) => <span className="font-mono font-bold text-slate-900">{formatCurrency(row.netRevenue)}</span> },
    { header: 'VAT Collected', accessor: 'vat', render: (row) => <span className="font-mono text-slate-500">+{formatCurrency(row.vat)}</span> },
    { header: 'Settlement Date', accessor: 'date', render: (row) => formatDate(row.date, true) }
  ];

  return (
    <div>
      <PageHeader
        title="ERP: Financial Accounting Reports &amp; Statements"
        subtitle="Audited financial summaries, Revenue &amp; Purchases recognition, settlement payment channels, and Net Profit &amp; Loss."
        actions={
          <button
            onClick={() => window.print()}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs"
          >
            <Printer className="w-3.5 h-3.5 text-blue-600" />
            Print Accounting Statement
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'pnl', label: 'Net Profit & Loss Statement', icon: LineChart },
          { id: 'sales', label: 'Recognized Sales Revenue', count: salesTransactions.length, icon: ArrowUpRight },
          { id: 'settlements', label: 'Payment Channels & Settlements', icon: DollarSign }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'pnl' && (
        <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-xs max-w-3xl">
          <div className="flex justify-between items-start pb-4 border-b border-slate-200 mb-6">
            <div>
              <h3 className="text-base font-bold text-slate-900">Statement of Comprehensive Income (P&amp;L)</h3>
              <p className="text-xs text-slate-500 mt-0.5">Period: January 01, 2026 – August 31, 2026 • Reporting Currency: NGN (₦)</p>
            </div>
            <span className="text-xs font-bold text-emerald-800 bg-emerald-50 px-3 py-1.5 rounded-xl border border-emerald-200">
              Net Margin: {pnl.marginPct}
            </span>
          </div>

          <div className="space-y-3 text-xs font-mono">
            {/* Revenue */}
            <div className="flex justify-between font-bold text-slate-900 text-sm pb-1">
              <span className="font-sans">Gross Commercial Revenue (Sales &amp; Services):</span>
              <span>{formatCurrency(pnl.grossRevenue)}</span>
            </div>
            <div className="flex justify-between text-rose-600 pl-4">
              <span className="font-sans">Less: Direct Cost of Goods &amp; Services (COGS):</span>
              <span>-{formatCurrency(pnl.costOfGoodsServices)}</span>
            </div>
            <div className="flex justify-between font-bold text-slate-900 pt-2 pb-2 border-t border-b border-slate-200">
              <span className="font-sans">Gross Operating Profit:</span>
              <span>{formatCurrency(pnl.grossProfit)}</span>
            </div>

            {/* Expenses */}
            <div className="pt-2 font-bold text-slate-700 font-sans">Operating Expenses (OPEX):</div>
            <div className="flex justify-between text-rose-600 pl-4">
              <span className="font-sans">Corporate Workforce Payroll &amp; Compensation:</span>
              <span>-{formatCurrency(pnl.payrollExpenses)}</span>
            </div>
            <div className="flex justify-between text-rose-600 pl-4">
              <span className="font-sans">General Administrative, Cloud &amp; Utilities:</span>
              <span>-{formatCurrency(pnl.operatingExpenses)}</span>
            </div>

            {/* EBITDA & Net */}
            <div className="flex justify-between font-bold text-slate-900 pt-3 pb-2 border-t border-slate-200">
              <span className="font-sans">Operating Income (EBITDA):</span>
              <span>{formatCurrency(pnl.ebitda)}</span>
            </div>
            <div className="flex justify-between text-rose-600 pl-4">
              <span className="font-sans">Estimated Company Income Tax (CIT Provision):</span>
              <span>-{formatCurrency(pnl.taxEstimate)}</span>
            </div>
            <div className="flex justify-between text-base font-bold text-emerald-800 pt-3 pb-2 border-t-2 border-slate-900">
              <span className="font-sans">Net Retained Profit:</span>
              <span>{formatCurrency(pnl.netProfit)}</span>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'sales' && (
        <DataTable
          columns={salesColumns}
          data={salesTransactions}
          searchPlaceholder="Search sales ledger..."
        />
      )}

      {activeTab === 'settlements' && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs">
            <h4 className="font-bold text-sm text-slate-900 mb-4">Inbound Settlements by Channel</h4>
            <div className="space-y-3 text-xs">
              <div className="flex justify-between p-3 rounded-xl bg-slate-50 border border-slate-100">
                <span className="font-semibold text-slate-700">Dedicated Virtual Accounts (DVA / NIP):</span>
                <span className="font-mono font-bold text-slate-900">{formatCurrency(24500000.0)} (71%)</span>
              </div>
              <div className="flex justify-between p-3 rounded-xl bg-slate-50 border border-slate-100">
                <span className="font-semibold text-slate-700">Online Card Checkout (3D-Secure):</span>
                <span className="font-mono font-bold text-slate-900">{formatCurrency(8150000.0)} (24%)</span>
              </div>
              <div className="flex justify-between p-3 rounded-xl bg-slate-50 border border-slate-100">
                <span className="font-semibold text-slate-700">Internal Peer Ledger Settlements:</span>
                <span className="font-mono font-bold text-slate-900">{formatCurrency(1850000.0)} (5%)</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
