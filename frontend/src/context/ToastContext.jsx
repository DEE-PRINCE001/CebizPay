import React, { createContext, useContext, useState, useCallback } from 'react';
import { CheckCircle2, AlertTriangle, XCircle, Info, X, Copy, Check } from 'lucide-react';

const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const [copiedId, setCopiedId] = useState(null);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(({ title, message, type = 'info', referenceId = null, duration = 6000 }) => {
    const id = Math.random().toString(36).substring(2, 9);
    const newToast = { id, title, message, type, referenceId };

    setToasts((prev) => [...prev, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        removeToast(id);
      }, duration);
    }
    return id;
  }, [removeToast]);

  const copyRef = (text, id) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  const showSuccess = useCallback((title, message, referenceId = null) => {
    return addToast({ title, message, type: 'success', referenceId });
  }, [addToast]);

  const showError = useCallback((title, message, referenceId = null) => {
    return addToast({ title, message, type: 'error', referenceId });
  }, [addToast]);

  const showWarning = useCallback((title, message, referenceId = null) => {
    return addToast({ title, message, type: 'warning', referenceId });
  }, [addToast]);

  const showInfo = useCallback((title, message, referenceId = null) => {
    return addToast({ title, message, type: 'info', referenceId });
  }, [addToast]);

  return (
    <ToastContext.Provider value={{ addToast, showSuccess, showError, showWarning, showInfo, removeToast }}>
      {children}
      
      {/* Toast Render Container */}
      <div className="fixed bottom-5 right-5 z-50 flex flex-col gap-2.5 max-w-md w-full pointer-events-none">
        {toasts.map((toast) => {
          let bg = 'bg-white border-slate-200 text-slate-800';
          let icon = <Info className="w-5 h-5 text-blue-600 shrink-0" />;

          if (toast.type === 'success') {
            bg = 'bg-emerald-50 border-emerald-200 text-emerald-950';
            icon = <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />;
          } else if (toast.type === 'error') {
            bg = 'bg-rose-50 border-rose-200 text-rose-950';
            icon = <XCircle className="w-5 h-5 text-rose-600 shrink-0" />;
          } else if (toast.type === 'warning') {
            bg = 'bg-amber-50 border-amber-200 text-amber-950';
            icon = <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0" />;
          }

          return (
            <div
              key={toast.id}
              className={`pointer-events-auto flex items-start gap-3 p-4 rounded-xl border shadow-lg transition-all transform translate-y-0 opacity-100 ${bg}`}
            >
              {icon}
              <div className="flex-1 min-w-0">
                {toast.title && <h4 className="text-sm font-semibold mb-0.5">{toast.title}</h4>}
                <p className="text-xs leading-relaxed opacity-90">{toast.message}</p>
                {toast.referenceId && (
                  <div className="mt-2 flex items-center gap-1.5 bg-black/5 rounded px-2 py-1 text-xs font-mono">
                    <span className="truncate">Ref: {toast.referenceId}</span>
                    <button
                      onClick={() => copyRef(toast.referenceId, toast.id)}
                      className="text-slate-600 hover:text-slate-900 transition-colors p-0.5"
                      title="Copy Reference"
                    >
                      {copiedId === toast.id ? <Check className="w-3.5 h-3.5 text-emerald-600" /> : <Copy className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                )}
              </div>
              <button
                onClick={() => removeToast(toast.id)}
                className="opacity-60 hover:opacity-100 transition-opacity p-1 text-slate-500"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
}
