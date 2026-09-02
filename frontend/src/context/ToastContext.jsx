import React, { createContext, useState, useCallback } from 'react';
import Toast from '../components/feedback/Toast';

export const ToastContext = createContext(null);

/**
 * Global Toast Notification Provider.
 */
export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const dismissToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const showToast = useCallback(({ type = 'info', title, message, duration = 4000 }) => {
    const id = `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const newToast = { id, type, title, message };

    setToasts((prev) => [...prev, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        dismissToast(id);
      }, duration);
    }

    return id;
  }, [dismissToast]);

  const showSuccess = useCallback((message, title = 'Success') => {
    return showToast({ type: 'success', title, message });
  }, [showToast]);

  const showError = useCallback((message, title = 'Error') => {
    return showToast({ type: 'error', title, message });
  }, [showToast]);

  const showWarning = useCallback((message, title = 'Warning') => {
    return showToast({ type: 'warning', title, message });
  }, [showToast]);

  const showInfo = useCallback((message, title = 'Info') => {
    return showToast({ type: 'info', title, message });
  }, [showToast]);

  return (
    <ToastContext.Provider
      value={{
        showToast,
        showSuccess,
        showError,
        showWarning,
        showInfo,
        dismissToast
      }}
    >
      {children}
      {/* Toast Floating Container */}
      <div className="fixed bottom-5 right-5 z-50 flex flex-col gap-2.5 pointer-events-none max-w-sm w-full">
        {toasts.map((toast) => (
          <Toast key={toast.id} {...toast} onDismiss={dismissToast} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}
