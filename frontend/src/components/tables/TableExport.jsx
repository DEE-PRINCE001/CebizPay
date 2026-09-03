import React, { useState, useRef, useEffect } from 'react';
import { Download, FileSpreadsheet, FileText, ChevronDown } from 'lucide-react';
import Button from '../common/Button';

/**
 * Data export dropdown menu.
 */
export default function TableExport({
  onExportCsv,
  onExportPdf,
  onExportExcel,
  label = 'Export',
  className = ''
}) {
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

  return (
    <div className={`relative inline-block text-left ${className}`} ref={menuRef}>
      <Button
        variant="outline"
        size="sm"
        onClick={() => setIsOpen(!isOpen)}
        className="gap-1.5"
      >
        <Download size={14} />
        <span>{label}</span>
        <ChevronDown size={14} />
      </Button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-44 rounded-2xl bg-white shadow-xl border border-slate-100 p-2 z-30 animate-in fade-in zoom-in-95">
          {onExportCsv && (
            <button
              type="button"
              onClick={() => {
                onExportCsv();
                setIsOpen(false);
              }}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-slate-700 hover:bg-slate-50 hover:text-slate-900 rounded-xl transition-colors text-left"
            >
              <FileSpreadsheet size={15} className="text-status-success" />
              <span>Export as CSV</span>
            </button>
          )}
          {onExportExcel && (
            <button
              type="button"
              onClick={() => {
                onExportExcel();
                setIsOpen(false);
              }}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-slate-700 hover:bg-slate-50 hover:text-slate-900 rounded-xl transition-colors text-left"
            >
              <FileSpreadsheet size={15} className="text-emerald-700" />
              <span>Export as Excel</span>
            </button>
          )}
          {onExportPdf && (
            <button
              type="button"
              onClick={() => {
                onExportPdf();
                setIsOpen(false);
              }}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs text-slate-700 hover:bg-slate-50 hover:text-slate-900 rounded-xl transition-colors text-left"
            >
              <FileText size={15} className="text-status-danger" />
              <span>Export as PDF</span>
            </button>
          )}
        </div>
      )}
    </div>
  );
}
