import React, { useState, useRef, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ROUTES } from '../../constants/routes';
import { ChevronDown, LogOut, KeyRound, Settings, UserCheck } from 'lucide-react';
import Badge from '../common/Badge';

/**
 * User profile avatar and menu dropdown.
 */
export default function UserMenu({ className = '' }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleLogout = async () => {
    setIsOpen(false);
    await logout();
    navigate(ROUTES.LOGIN, { replace: true });
  };

  const fullName = user?.fullName || `${user?.firstName || ''} ${user?.lastName || ''}`.trim() || 'User Account';
  const initials = fullName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((n) => n[0].toUpperCase())
    .join('') || 'U';

  const roleLabel = user?.role || 'Member';

  return (
    <div className={`relative inline-block text-left ${className}`} ref={menuRef}>
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 p-1 pl-1.5 pr-2.5 rounded-full border border-slate-200 bg-white hover:bg-slate-50 transition-all text-xs font-semibold text-slate-800 shadow-2xs"
      >
        <div className="w-7 h-7 rounded-full bg-slate-900 text-white flex items-center justify-center font-bold text-[11px] shrink-0">
          {initials}
        </div>
        <span className="hidden sm:inline max-w-[120px] truncate">{fullName}</span>
        <ChevronDown size={14} className="text-slate-400 shrink-0" />
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-64 rounded-2xl bg-white shadow-xl border border-slate-100 p-2 z-50 animate-in fade-in zoom-in-95">
          {/* User Info Header */}
          <div className="px-3 py-2.5 border-b border-slate-100">
            <div className="flex items-center justify-between mb-1">
              <div className="font-bold text-sm text-slate-900 truncate">{fullName}</div>
              <Badge variant="brand" size="sm">
                {roleLabel}
              </Badge>
            </div>
            <div className="text-xs text-slate-500 truncate">{user?.email}</div>
          </div>

          {/* Menu Items */}
          <div className="py-1 space-y-0.5">
            <Link
              to={ROUTES.SETTINGS}
              onClick={() => setIsOpen(false)}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-slate-700 hover:bg-slate-50 rounded-xl transition-colors"
            >
              <Settings size={15} className="text-slate-500" />
              <span>Account & Security Settings</span>
            </Link>

            <Link
              to={ROUTES.CHANGE_PASSWORD}
              onClick={() => setIsOpen(false)}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-slate-700 hover:bg-slate-50 rounded-xl transition-colors"
            >
              <KeyRound size={15} className="text-slate-500" />
              <span>Change Password</span>
            </Link>

            {user?.role === 'SuperAdmin' && (
              <Link
                to={ROUTES.ADMIN_DASHBOARD}
                onClick={() => setIsOpen(false)}
                className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-brand-600 hover:bg-brand-50 rounded-xl font-medium transition-colors"
              >
                <UserCheck size={15} />
                <span>SuperAdmin Console</span>
              </Link>
            )}
          </div>

          {/* Logout Action */}
          <div className="pt-1 mt-1 border-t border-slate-100">
            <button
              type="button"
              onClick={handleLogout}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-red-600 hover:bg-red-50 font-medium rounded-xl transition-colors text-left"
            >
              <LogOut size={15} />
              <span>Sign Out</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
