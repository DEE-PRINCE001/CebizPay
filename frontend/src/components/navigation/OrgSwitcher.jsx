import React, { useState, useRef, useEffect } from 'react';
import { Building2, ChevronDown, Check, Plus } from 'lucide-react';
import { useOrg } from '../../context/OrgContext';
import { Link } from 'react-router-dom';
import { ROUTES } from '../../constants/routes';

/**
 * Organization tenant switcher dropdown.
 */
export default function OrgSwitcher({ className = '' }) {
  const { currentOrg, currentOrgId, organizations, switchOrganization } = useOrg();
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const displayName = currentOrg?.name || 'My Organization';

  return (
    <div className={`relative inline-block text-left ${className}`} ref={dropdownRef}>
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 px-3 py-1.5 rounded-full border border-slate-200 bg-white hover:bg-slate-50 transition-all text-xs font-semibold text-slate-800 shadow-2xs"
      >
        <div className="w-5 h-5 rounded-full bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
          <Building2 size={12} />
        </div>
        <span className="max-w-[140px] truncate">{displayName}</span>
        <ChevronDown size={14} className="text-slate-400 shrink-0" />
      </button>

      {isOpen && (
        <div className="absolute left-0 mt-2 w-64 rounded-2xl bg-white shadow-xl border border-slate-100 p-2 z-50 animate-in fade-in zoom-in-95">
          <div className="px-3 py-2 border-b border-slate-100">
            <span className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
              Current Organization
            </span>
            <div className="font-bold text-xs text-slate-900 truncate mt-0.5">
              {displayName}
            </div>
          </div>

          <div className="py-1 max-h-48 overflow-y-auto">
            {organizations.length > 0 ? (
              organizations.map((org) => {
                const isSelected = org.id === currentOrgId;
                return (
                  <button
                    key={org.id}
                    type="button"
                    onClick={() => {
                      switchOrganization(org.id, org);
                      setIsOpen(false);
                    }}
                    className={`w-full flex items-center justify-between px-3 py-2 text-xs rounded-xl transition-colors ${
                      isSelected
                        ? 'bg-brand-50 text-brand-600 font-semibold'
                        : 'text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    <span className="truncate">{org.name}</span>
                    {isSelected && <Check size={14} className="text-brand-600 shrink-0" />}
                  </button>
                );
              })
            ) : (
              <div className="px-3 py-2 text-xs text-slate-500">
                Primary active workspace
              </div>
            )}
          </div>

          <div className="pt-1 mt-1 border-t border-slate-100">
            <Link
              to={ROUTES.KYB_VERIFICATION}
              onClick={() => setIsOpen(false)}
              className="w-full flex items-center gap-2 px-3 py-2 text-xs text-brand-600 hover:bg-brand-50 font-medium rounded-xl transition-colors"
            >
              <Plus size={14} />
              <span>Verify / Register Organization</span>
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
