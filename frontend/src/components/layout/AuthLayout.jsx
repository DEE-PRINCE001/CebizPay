import React from 'react';
import { Outlet, Link } from 'react-router-dom';

export default function AuthLayout() {
  return (
    <div className="min-h-screen bg-slate-50 flex flex-col justify-between p-4 sm:p-6 lg:p-8">
      {/* Top Header */}
      <header className="max-w-6xl w-full mx-auto flex items-center justify-between py-4">
        <Link to="/" className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-blue-600 text-white flex items-center justify-center font-black text-lg tracking-wider shadow-xs shadow-blue-500/20">
            C
          </div>
          <div>
            <span className="font-extrabold text-base tracking-tight text-slate-900 block leading-tight">
              CebizPay
            </span>
            <span className="text-[10px] font-semibold text-slate-400 uppercase tracking-widest block">
              Fintech &amp; ERP
            </span>
          </div>
        </Link>
        <div className="flex items-center gap-4 text-xs font-semibold">
          <Link to="/careers" className="text-slate-600 hover:text-slate-900">
            Public Careers
          </Link>
          <Link to="/login" className="px-3.5 py-1.5 rounded-xl bg-white border border-slate-200 text-slate-800 hover:bg-slate-50 shadow-xs">
            Sign In
          </Link>
        </div>
      </header>

      {/* Center Auth Content */}
      <main className="flex-1 flex items-center justify-center py-8">
        <Outlet />
      </main>

      {/* Footer */}
      <footer className="max-w-6xl w-full mx-auto text-center py-4 border-t border-slate-200/60 text-xs text-slate-400">
        <p>© 2026 CebizPay Technologies Limited. Fully licensed &amp; regulated multi-tenant financial platform.</p>
      </footer>
    </div>
  );
}
