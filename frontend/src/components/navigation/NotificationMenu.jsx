import React, { useState, useRef, useEffect } from 'react';
import { Bell, CheckCircle2, AlertCircle, Info, X } from 'lucide-react';

/**
 * Notification Bell Menu Popover matching Dashboard.png (D085).
 */
export default function NotificationMenu({ className = '' }) {
  const [isOpen, setIsOpen] = useState(false);
  const [notifications, setNotifications] = useState([
    {
      id: '1',
      type: 'info',
      title: 'Ledger Settlement',
      message: 'Daily settlement completed for operating wallet.',
      time: '10m ago',
      read: false
    },
    {
      id: '2',
      type: 'success',
      title: 'Payroll Processed',
      message: 'Batch salary vouchers disbursed successfully.',
      time: '1h ago',
      read: false
    }
  ]);
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

  const unreadCount = notifications.filter((n) => !n.read).length;

  const markAllRead = () => {
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })));
  };

  const removeNotification = (id) => {
    setNotifications((prev) => prev.filter((n) => n.id !== id));
  };

  const getIcon = (type) => {
    switch (type) {
      case 'success':
        return <CheckCircle2 size={14} className="text-status-success shrink-0" />;
      case 'warning':
      case 'error':
        return <AlertCircle size={14} className="text-status-danger shrink-0" />;
      default:
        return <Info size={14} className="text-brand-600 shrink-0" />;
    }
  };

  return (
    <div className={`relative inline-block text-left ${className}`} ref={menuRef}>
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="relative p-2 rounded-full border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors shadow-2xs"
        aria-label="Notifications"
      >
        <Bell size={17} />
        {unreadCount > 0 && (
          <span className="absolute top-1 right-1 w-2 h-2 rounded-full bg-status-danger ring-2 ring-white" />
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-80 rounded-2xl bg-white shadow-xl border border-slate-100 p-3 z-50 animate-in fade-in zoom-in-95">
          <div className="flex items-center justify-between pb-2 mb-2 border-b border-slate-100">
            <div className="flex items-center gap-1.5">
              <span className="text-xs font-bold text-slate-900">Notifications</span>
              {unreadCount > 0 && (
                <span className="text-[10px] font-bold px-1.5 py-0.2 bg-brand-50 text-brand-600 rounded-full">
                  {unreadCount} new
                </span>
              )}
            </div>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={markAllRead}
                className="text-[11px] text-brand-600 hover:underline font-medium"
              >
                Mark all read
              </button>
            )}
          </div>

          <div className="space-y-1.5 max-h-64 overflow-y-auto">
            {notifications.length > 0 ? (
              notifications.map((n) => (
                <div
                  key={n.id}
                  className={`p-2.5 rounded-xl text-xs flex items-start justify-between gap-2 transition-colors ${
                    n.read ? 'bg-white hover:bg-slate-50 text-slate-600' : 'bg-brand-50/50 text-slate-900'
                  }`}
                >
                  <div className="flex items-start gap-2 min-w-0">
                    <div className="mt-0.5">{getIcon(n.type)}</div>
                    <div className="min-w-0">
                      <div className="font-semibold text-slate-900 truncate">{n.title}</div>
                      <p className="text-[11px] text-slate-500 line-clamp-2 mt-0.5">{n.message}</p>
                      <span className="text-[10px] text-slate-400 mt-1 block">{n.time}</span>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => removeNotification(n.id)}
                    className="text-slate-300 hover:text-slate-600 p-0.5 rounded-md"
                  >
                    <X size={12} />
                  </button>
                </div>
              ))
            ) : (
              <div className="text-center py-6 text-xs text-slate-400">
                No notifications right now
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
