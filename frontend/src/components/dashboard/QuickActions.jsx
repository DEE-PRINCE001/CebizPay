import React from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import Card from '../common/Card';
import Button from '../common/Button';
import {
  Plus,
  ArrowUpRight,
  Receipt,
  Zap,
  UserPlus,
  FileText
} from 'lucide-react';

/**
 * Quick action triggers for common operations.
 */
export default function QuickActions({
  onFundWallet,
  onTransfer,
  className = ''
}) {
  const navigate = useNavigate();

  return (
    <Card padding="p-4" className={`space-y-3 ${className}`}>
      <div className="flex items-center justify-between">
        <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
          Quick Actions
        </span>
      </div>

      <div className="flex items-center gap-2.5 overflow-x-auto no-scrollbar py-1">
        <Button
          variant="primary"
          size="sm"
          icon={Plus}
          onClick={onFundWallet}
          className="shrink-0"
        >
          Fund Wallet
        </Button>

        <Button
          variant="secondary"
          size="sm"
          icon={ArrowUpRight}
          onClick={onTransfer}
          className="shrink-0"
        >
          Transfer Funds
        </Button>

        <Button
          variant="outline"
          size="sm"
          icon={Receipt}
          onClick={() => navigate(ROUTES.PAYROLL)}
          className="shrink-0"
        >
          Process Payroll
        </Button>

        <Button
          variant="outline"
          size="sm"
          icon={Zap}
          onClick={() => navigate(ROUTES.VAS)}
          className="shrink-0"
        >
          Buy Airtime / Data
        </Button>

        <Button
          variant="outline"
          size="sm"
          icon={UserPlus}
          onClick={() => navigate(ROUTES.STAFF)}
          className="shrink-0"
        >
          Add Staff
        </Button>

        <Button
          variant="outline"
          size="sm"
          icon={FileText}
          onClick={() => navigate(ROUTES.INVOICES)}
          className="shrink-0"
        >
          Create Invoice
        </Button>
      </div>
    </Card>
  );
}
