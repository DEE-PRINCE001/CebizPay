import React from 'react';
import { Link } from 'react-router-dom';
import { ROUTES } from '../constants/routes';

/**
 * Authentication layout shell.
 */
export default function AuthLayout({
  children,
  title,
  subtitle,
  footer
}) {
  return (
    <div className="min-h-screen bg-canvas-bg flex flex-col justify-between p-4 sm:p-6 lg:p-8">
      {/* Top Brand Bar */}
      <div className="max-w-7xl w-full mx-auto flex items-center justify-between py-2">
        <Link to={ROUTES.HOME} className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-brand-600 flex items-center justify-center text-white font-bold text-base shadow-xs shadow-brand-500/20">
            CP
          </div>
          <span className="text-xl font-bold text-slate-900 tracking-tight">
            Cebiz<span className="text-brand-600">Pay</span>
          </span>
        </Link>

        <div className="text-xs text-slate-500 hidden sm:block">
          Need assistance?{' '}
          <a href="mailto:support@cebizpay.com" className="text-brand-600 hover:underline font-medium">
            Contact Support
          </a>
        </div>
      </div>

      {/* Centered Auth Card */}
      <div className="my-auto py-8 flex items-center justify-center">
        <div className="w-full max-w-md bg-white rounded-2xl border border-slate-100 shadow-[0_2px_12px_rgba(0,0,0,0.04)] p-6 sm:p-8">
          {(title || subtitle) && (
            <div className="text-center mb-6">
              {title && <h1 className="text-2xl font-bold text-slate-900 tracking-tight">{title}</h1>}
              {subtitle && <p className="text-xs text-slate-500 mt-1.5 leading-relaxed">{subtitle}</p>}
            </div>
          )}

          <div className="space-y-4">
            {children}
          </div>

          {footer && (
            <div className="mt-6 pt-4 border-t border-slate-100 text-center text-xs text-slate-500">
              {footer}
            </div>
          )}
        </div>
      </div>

      {/* Bottom Legal / Copyright Bar */}
      <div className="max-w-7xl w-full mx-auto flex flex-col sm:flex-row items-center justify-between gap-2 py-3 text-xs text-slate-400 border-t border-slate-200/60">
        <div>
          &copy; {new Date().getFullYear()} CebizPay Financial Technologies Inc. All rights reserved.
        </div>
        <div className="flex items-center gap-4">
          <Link to="#" className="hover:text-slate-600">Privacy Policy</Link>
          <Link to="#" className="hover:text-slate-600">Terms of Service</Link>
          <Link to="#" className="hover:text-slate-600">Security Overview</Link>
        </div>
      </div>
    </div>
  );
}
