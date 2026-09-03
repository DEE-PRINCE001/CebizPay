import React from 'react';
import { BrowserRouter, Routes, Route, Link, Navigate } from 'react-router-dom';
import { ROUTES } from './constants/routes';
import { AuthProvider } from './context/AuthContext';
import { OrgProvider } from './context/OrgContext';
import { ToastProvider } from './context/ToastContext';
import ProtectedRoute from './components/navigation/ProtectedRoute';

// Shell Layouts
import CustomerLayout from './layouts/CustomerLayout';
import ErpLayout from './layouts/ErpLayout';
import AdminLayout from './layouts/AdminLayout';
import MarketingLayout from './layouts/MarketingLayout';

// Auth Pages
import LoginPage from './pages/auth/LoginPage';
import RegisterPhonePage from './pages/auth/RegisterPhonePage';
import VerifyOtpPage from './pages/auth/VerifyOtpPage';
import ChangePasswordPage from './pages/auth/ChangePasswordPage';
import RedeemAdminInvitePage from './pages/auth/RedeemAdminInvitePage';
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage';

// Customer Pages
import DashboardPage from './pages/customer/DashboardPage';
import WalletPage from './pages/customer/WalletPage';
import TransfersPage from './pages/customer/TransfersPage';
import CardsPage from './pages/customer/CardsPage';
import VasPage from './pages/customer/VasPage';
import PayrollPage from './pages/customer/PayrollPage';
import StaffPage from './pages/customer/StaffPage';
import SavingsPage from './pages/customer/SavingsPage';
import SettingsPage from './pages/customer/SettingsPage';

// ERP Pages
import InventoryPage from './pages/erp/InventoryPage';
import ServicesPage from './pages/erp/ServicesPage';
import InvoicesPage from './pages/erp/InvoicesPage';
import SalesPage from './pages/erp/SalesPage';
import PurchasesPage from './pages/erp/PurchasesPage';
import ExpensesPage from './pages/erp/ExpensesPage';
import CustomersPage from './pages/erp/CustomersPage';
import SuppliersPage from './pages/erp/SuppliersPage';

// SuperAdmin Pages
import AdminDashboardPage from './pages/admin/AdminDashboardPage';
import AdminUsersPage from './pages/admin/AdminUsersPage';
import AdminCompliancePage from './pages/admin/AdminCompliancePage';
import AdminFeesPage from './pages/admin/AdminFeesPage';
import AdminReconciliationPage from './pages/admin/AdminReconciliationPage';
import AdminAuditLogsPage from './pages/admin/AdminAuditLogsPage';

function PublicLanding() {
  return (
    <MarketingLayout>
      <div className="max-w-5xl mx-auto px-4 py-20 text-center space-y-6">
        <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-brand-50 border border-brand-200 text-xs font-semibold text-brand-700">
          <span>Enterprise Fintech & Workforce Infrastructure</span>
        </div>
        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold text-slate-900 tracking-tight leading-tight">
          Modern Financial Operations for <span className="text-brand-600">High-Growth Business</span>
        </h1>
        <p className="text-base sm:text-lg text-slate-600 max-w-2xl mx-auto leading-relaxed">
          Automate bulk payroll, multi-tier corporate wallets, ERP catalog operations, and instant merchant collections through a single unified ledger.
        </p>
        <div className="flex flex-col sm:flex-row items-center justify-center gap-4 pt-4">
          <Link
            to={ROUTES.REGISTER_PHONE}
            className="w-full sm:w-auto px-8 py-3.5 bg-brand-600 hover:bg-brand-700 text-white font-semibold text-sm rounded-full shadow-sm shadow-brand-500/20 transition"
          >
            Create Business Account
          </Link>
          <Link
            to={ROUTES.LOGIN}
            className="w-full sm:w-auto px-8 py-3.5 border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-sm rounded-full transition"
          >
            Sign In to Portal
          </Link>
        </div>
      </div>
    </MarketingLayout>
  );
}

export default function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <OrgProvider>
          <BrowserRouter>
            <Routes>
              {/* Public Routes */}
              <Route path={ROUTES.HOME} element={<PublicLanding />} />
              <Route path={ROUTES.CAREERS} element={<PublicLanding />} />

              {/* Authentication Routes */}
              <Route path={ROUTES.LOGIN} element={<LoginPage />} />
              <Route path={ROUTES.REGISTER_PHONE} element={<RegisterPhonePage />} />
              <Route path={ROUTES.VERIFY_OTP} element={<VerifyOtpPage />} />
              <Route path={ROUTES.CHANGE_PASSWORD} element={<ChangePasswordPage />} />
              <Route path={ROUTES.REDEEM_INVITE} element={<RedeemAdminInvitePage />} />
              <Route path={ROUTES.FORGOT_PASSWORD} element={<ForgotPasswordPage />} />

              {/* Protected Customer Routes */}
              <Route
                path={ROUTES.DASHBOARD}
                element={
                  <ProtectedRoute>
                    <DashboardPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.WALLET}
                element={
                  <ProtectedRoute>
                    <WalletPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.TRANSFERS}
                element={
                  <ProtectedRoute>
                    <TransfersPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.CARDS}
                element={
                  <ProtectedRoute>
                    <CardsPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.VAS}
                element={
                  <ProtectedRoute>
                    <VasPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.PAYROLL}
                element={
                  <ProtectedRoute>
                    <PayrollPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.STAFF}
                element={
                  <ProtectedRoute>
                    <StaffPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.SAVINGS}
                element={
                  <ProtectedRoute>
                    <SavingsPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.SETTINGS}
                element={
                  <ProtectedRoute>
                    <SettingsPage />
                  </ProtectedRoute>
                }
              />

              {/* Protected ERP Module Routes */}
              <Route
                path={ROUTES.INVENTORY}
                element={
                  <ProtectedRoute>
                    <InventoryPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.SERVICES}
                element={
                  <ProtectedRoute>
                    <ServicesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.INVOICES}
                element={
                  <ProtectedRoute>
                    <InvoicesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.SALES}
                element={
                  <ProtectedRoute>
                    <SalesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.PURCHASES}
                element={
                  <ProtectedRoute>
                    <PurchasesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.EXPENSES}
                element={
                  <ProtectedRoute>
                    <ExpensesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.CUSTOMERS}
                element={
                  <ProtectedRoute>
                    <CustomersPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.SUPPLIERS}
                element={
                  <ProtectedRoute>
                    <SuppliersPage />
                  </ProtectedRoute>
                }
              />

              {/* Protected SuperAdmin Routes */}
              <Route
                path={ROUTES.ADMIN_DASHBOARD}
                element={
                  <ProtectedRoute allowedRoles={['SuperAdmin', 'Admin']}>
                    <AdminDashboardPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.ADMIN_USERS}
                element={
                  <ProtectedRoute allowedRoles={['SuperAdmin', 'Admin']}>
                    <AdminUsersPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.ADMIN_COMPLIANCE}
                element={
                  <ProtectedRoute allowedRoles={['SuperAdmin', 'Admin']}>
                    <AdminCompliancePage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.ADMIN_FEES}
                element={
                  <ProtectedRoute allowedRoles={['SuperAdmin', 'Admin']}>
                    <AdminFeesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.ADMIN_RECONCILIATION}
                element={
                  <ProtectedRoute allowedRoles={['SuperAdmin', 'Admin']}>
                    <AdminReconciliationPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path={ROUTES.ADMIN_AUDIT_LOGS}
                element={
                  <ProtectedRoute allowedRoles={['SuperAdmin', 'Admin']}>
                    <AdminAuditLogsPage />
                  </ProtectedRoute>
                }
              />

              {/* 404 Catch-All */}
              <Route path="*" element={<Navigate to={ROUTES.HOME} replace />} />
            </Routes>
          </BrowserRouter>
        </OrgProvider>
      </AuthProvider>
    </ToastProvider>
  );
}
