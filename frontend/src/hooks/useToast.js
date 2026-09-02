import { useContext } from 'react';
import { ToastContext } from '../context/ToastContext';

/**
 * Custom Hook to access global toast notifications.
 */
export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
}
