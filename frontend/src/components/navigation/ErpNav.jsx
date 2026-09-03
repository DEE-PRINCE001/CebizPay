import React from 'react';
import { NavLink } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import {
  Boxes,
  Briefcase,
  FileText,
  ShoppingCart,
  Receipt,
  Users2,
  Truck,
  TrendingDown
} from 'lucide-react';

/**
 * ERP module navigation tabs.
 */
export default function ErpNav({ className = '' }) {
  const erpItems = [
    { to: ROUTES.INVENTORY, label: 'Inventory & Items', icon: Boxes },
    { to: ROUTES.SERVICES, label: 'Services', icon: Briefcase },
    { to: ROUTES.INVOICES, label: 'Invoices', icon: FileText },
    { to: ROUTES.SALES, label: 'Sales Orders', icon: ShoppingCart },
    { to: ROUTES.PURCHASES, label: 'Purchases', icon: Receipt },
    { to: ROUTES.EXPENSES, label: 'Expenses', icon: TrendingDown },
    { to: ROUTES.CUSTOMERS, label: 'Customers', icon: Users2 },
    { to: ROUTES.SUPPLIERS, label: 'Suppliers', icon: Truck }
  ];

  return (
    <div className={`flex items-center gap-2 overflow-x-auto no-scrollbar py-2 border-b border-slate-200/80 mb-6 ${className}`}>
      {erpItems.map((item) => {
        const Icon = item.icon;
        return (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === ROUTES.INVENTORY}
            className={({ isActive }) =>
              `inline-flex items-center gap-2 px-3.5 py-1.5 rounded-xl text-xs font-semibold transition-all whitespace-nowrap select-none ${
                isActive
                  ? 'bg-slate-900 text-white shadow-xs'
                  : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50 hover:text-slate-900'
              }`
            }
          >
            <Icon size={14} />
            <span>{item.label}</span>
          </NavLink>
        );
      })}
    </div>
  );
}
