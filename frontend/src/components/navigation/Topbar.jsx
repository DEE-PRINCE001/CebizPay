import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';
import OrgSwitcher from './OrgSwitcher';
import UserMenu from './UserMenu';
import NotificationMenu from './NotificationMenu';
import SearchInput from '../forms/SearchInput';
import Badge from '../common/Badge';

/**
 * Global topbar header navigation.
 */
export default function Topbar({ isAdmin = false, className = '' }) {
  const [searchQuery, setSearchQuery] = useState('');
  const navigate = useNavigate();

  const handleSearch = (e) => {
    e.preventDefault();
    if (searchQuery.trim()) {
      // Search handler
    }
  };

  return (
    <header className={`sticky top-0 z-40 bg-white/95 backdrop-blur-xs border-b border-slate-200/80 px-4 sm:px-6 lg:px-8 py-2.5 transition-all ${className}`}>
      <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
        {/* Left: Brand + Tenant Switcher / Admin Badge */}
        <div className="flex items-center gap-4 shrink-0">
          <Link to={isAdmin ? ROUTES.ADMIN_DASHBOARD : ROUTES.DASHBOARD} className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-xl bg-brand-600 flex items-center justify-center text-white font-bold text-sm shadow-xs shadow-brand-500/20">
              CP
            </div>
            <span className="text-lg font-bold text-slate-900 tracking-tight hidden sm:inline">
              Cebiz<span className="text-brand-600">Pay</span>
            </span>
          </Link>

          {isAdmin ? (
            <Badge variant="brand" size="sm" className="hidden xs:inline-flex">
              Admin Console
            </Badge>
          ) : (
            <OrgSwitcher />
          )}
        </div>

        {/* Middle: Universal Search Bar */}
        <div className="hidden md:flex flex-1 max-w-md mx-4">
          <form onSubmit={handleSearch} className="w-full">
            <SearchInput
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              onClear={() => setSearchQuery('')}
              placeholder={isAdmin ? 'Search users, logs, transactions...' : 'Search wallet, staff, invoices, items...'}
              className="max-w-full"
            />
          </form>
        </div>

        {/* Right: Actions, Notifications, User Menu */}
        <div className="flex items-center gap-2.5 shrink-0">
          <NotificationMenu />
          <UserMenu />
        </div>
      </div>
    </header>
  );
}
