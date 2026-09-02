import React from 'react';
import { Link } from 'react-router-dom';
import { ROUTES } from '../constants/routes';

/**
 * Public Marketing and Informational Shell Layout.
 */
export default function MarketingLayout({ children, className = '' }) {
  return (
    <div className="min-h-screen bg-canvas-bg flex flex-col justify-between">
      {/* Top Header */}
      <header className="sticky top-0 z-40 bg-white/90 backdrop-blur-xs border-b border-slate-200/80 px-4 sm:px-6 lg:px-8 py-3">
        <div className="max-w-7xl mx-auto flex items-center justify-between">
          <Link to={ROUTES.HOME} className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-brand-600 flex items-center justify-center text-white font-bold text-base shadow-xs shadow-brand-500/20">
              CP
            </div>
            <span className="text-xl font-bold text-slate-900 tracking-tight">
              Cebiz<span className="text-brand-600">Pay</span>
            </span>
          </Link>

          <div className="flex items-center gap-3">
            <Link
              to={ROUTES.CAREERS}
              className="text-xs font-semibold text-slate-600 hover:text-slate-900 px-3 py-1.5 rounded-full hover:bg-slate-100 transition hidden sm:inline-block"
            >
              Careers
            </Link>
            <Link
              to={ROUTES.LOGIN}
              className="px-4 py-2 text-xs font-semibold text-slate-700 hover:text-slate-900 hover:bg-slate-100 rounded-full transition"
            >
              Sign In
            </Link>
            <Link
              to={ROUTES.REGISTER_PHONE}
              className="px-4 py-2 text-xs font-semibold bg-brand-600 hover:bg-brand-700 text-white rounded-full transition shadow-xs shadow-brand-500/20"
            >
              Get Started
            </Link>
          </div>
        </div>
      </header>

      {/* Body Content */}
      <main className={`flex-1 ${className}`}>
        {children}
      </main>

      {/* Footer */}
      <footer className="bg-white border-t border-slate-200/80 py-8 px-4 sm:px-6 lg:px-8 text-xs text-slate-400">
        <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div>
            &copy; {new Date().getFullYear()} CebizPay Financial Technologies Inc. All rights reserved.
          </div>
          <div className="flex items-center gap-6">
            <Link to={ROUTES.CAREERS} className="hover:text-slate-600">Careers</Link>
            <Link to="#" className="hover:text-slate-600">Privacy Policy</Link>
            <Link to="#" className="hover:text-slate-600">Terms of Service</Link>
            <Link to="#" className="hover:text-slate-600">Compliance</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
