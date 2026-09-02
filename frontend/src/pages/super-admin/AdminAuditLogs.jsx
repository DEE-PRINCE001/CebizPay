import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { adminApi } from '../../api/adminApi';
import { Shield, Search, Filter, Terminal, User, FileCode } from 'lucide-react';

export default function AdminAuditLogs() {
  const [logs, setLogs] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchAuditLogs = async () => {
    setIsLoading(true);
    try {
      const res = await adminApi.getAuditLogs();
      if (Array.isArray(res)) setLogs(res);
      else if (res?.items && Array.isArray(res.items)) setLogs(res.items);
      else setLogs([]);
    } catch (err) {
      setLogs([]);
      console.warn('Backend audit logs fetch:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchAuditLogs();
  }, []);

  const columns = [
    {
      header: 'Audit Event ID & Action',
      accessor: 'id',
      render: (row) => (
        <div>
          <span className="font-mono font-bold text-slate-900 block">{row.action}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.id}</span>
        </div>
      ),
    },
    {
      header: 'Actor',
      accessor: 'actorEmail',
      render: (row) => (
        <div>
          <span className="font-semibold text-slate-900 text-xs block">{row.actorEmail || row.actorId}</span>
          <span className="text-[10px] text-slate-400 font-mono">{row.ipAddress}</span>
        </div>
      ),
    },
    {
      header: 'Resource Target',
      accessor: 'resourceType',
      render: (row) => (
        <div>
          <span className="text-xs font-bold text-slate-800 block">{row.resourceType}</span>
          <span className="text-[10px] text-slate-400 font-mono truncate max-w-xs block">{row.resourceId}</span>
        </div>
      ),
    },
    {
      header: 'Correlation ID',
      accessor: 'correlationId',
      render: (row) => <span className="font-mono text-slate-500 text-xs">{row.correlationId}</span>,
    },
    {
      header: 'Timestamp (UTC)',
      accessor: 'timestamp',
      render: (row) => formatDate(row.timestamp, true),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Central Audit Trail &amp; Ledger Telemetry"
        subtitle="Cryptographically tamper-evident, append-only logs capturing all financial mutations, compliance decisions, and admin actions."
      />

      <DataTable
        columns={columns}
        data={logs}
        searchPlaceholder="Search audit trail by actor, action, or resource ID..."
      />
    </div>
  );
}
