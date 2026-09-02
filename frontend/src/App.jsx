import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth, ROLES } from './context/AuthContext';
import { ToastProvider } from './context/ToastContext';

// Layouts
import AppLayout from './components/layout/AppLayout';
import AuthLayout from './components/layout/AuthLayout';

// Auth Pages
import LoginPage from './pages/auth/LoginPage';
import RegisterPage from './pages/auth/RegisterPage';

// Super Admin Pages
import AdminDashboard from './pages/super-admin/AdminDashboard';
import AdminCompliance from './pages/super-admin/AdminCompliance';
import AdminFeePolicies from './pages/super-admin/AdminFeePolicies';
import AdminSavingsPolicies from './pages/super-admin/AdminSavingsPolicies';
import AdminReconciliation from './pages/super-admin/AdminReconciliation';
import AdminAuditLogs from './pages/super-admin/AdminAuditLogs';
import AdminGovernance from './pages/super-admin/AdminGovernance';

// Organization Pages
import OrgDashboard from './pages/organization/OrgDashboard';
import OrgKybOnboarding from './pages/organization/OrgKybOnboarding';
import OrgStaff from './pages/organization/OrgStaff';
import OrgDepartmentsRoles from './pages/organization/OrgDepartmentsRoles';
import OrgPayroll from './pages/organization/OrgPayroll';
import OrgLoans from './pages/organization/OrgLoans';
import OrgSavings from './pages/organization/OrgSavings';
import OrgRecruitment from './pages/organization/OrgRecruitment';
import OrgERPInventory from './pages/organization/OrgERPInventory';
import OrgERPServices from './pages/organization/OrgERPServices';
import OrgERPCrm from './pages/organization/OrgERPCrm';
import OrgERPOrders from './pages/organization/OrgERPOrders';
import OrgERPExpenses from './pages/organization/OrgERPExpenses';
import OrgERPInvoices from './pages/organization/OrgERPInvoices';
import OrgERPVouchers from './pages/organization/OrgERPVouchers';
import OrgERPReports from './pages/organization/OrgERPReports';

// Consumer / Staff Pages
import WalletDashboard from './pages/consumer/WalletDashboard';
import TransfersPage from './pages/consumer/TransfersPage';
import CardFundingPage from './pages/consumer/CardFundingPage';
import VasPage from './pages/consumer/VasPage';
import KycCompliancePage from './pages/consumer/KycCompliancePage';
import WorkDashboard from './pages/consumer/WorkDashboard';
import WorkLoansPage from './pages/consumer/WorkLoansPage';
import WorkSavingsPage from './pages/consumer/WorkSavingsPage';
import ThriftPage from './pages/consumer/ThriftPage';

// Public Pages
import PublicCareersPage from './pages/public/PublicCareersPage';

function RootRedirect() {
  const { activeRole, isAuthenticated } = useAuth();
  if (activeRole === ROLES.SUPER_ADMIN) {
    return <Navigate to="/admin" replace />;
  }
  if (activeRole === ROLES.ORGANIZATION) {
    return <Navigate to="/org" replace />;
  }
  return <Navigate to="/consumer" replace />;
}

export default function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            {/* Auth Layout */}
            <Route element={<AuthLayout />}>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/careers" element={<PublicCareersPage />} />
            </Route>

            {/* App Layout with Multi-Role Surfaces */}
            <Route element={<AppLayout />}>
              <Route path="/" element={<RootRedirect />} />

              {/* Super Admin Routes */}
              <Route path="/admin" element={<AdminDashboard />} />
              <Route path="/admin/compliance" element={<AdminCompliance />} />
              <Route path="/admin/fees" element={<AdminFeePolicies />} />
              <Route path="/admin/savings-policies" element={<AdminSavingsPolicies />} />
              <Route path="/admin/reconciliation" element={<AdminReconciliation />} />
              <Route path="/admin/audit-logs" element={<AdminAuditLogs />} />
              <Route path="/admin/governance" element={<AdminGovernance />} />

              {/* Organization (B2B) Routes */}
              <Route path="/org" element={<OrgDashboard />} />
              <Route path="/org/kyb" element={<OrgKybOnboarding />} />
              <Route path="/org/staff" element={<OrgStaff />} />
              <Route path="/org/departments" element={<OrgDepartmentsRoles />} />
              <Route path="/org/payroll" element={<OrgPayroll />} />
              <Route path="/org/loans" element={<OrgLoans />} />
              <Route path="/org/savings" element={<OrgSavings />} />
              <Route path="/org/recruitment" element={<OrgRecruitment />} />
              <Route path="/org/erp/inventory" element={<OrgERPInventory />} />
              <Route path="/org/erp/services" element={<OrgERPServices />} />
              <Route path="/org/erp/crm" element={<OrgERPCrm />} />
              <Route path="/org/erp/orders" element={<OrgERPOrders />} />
              <Route path="/org/erp/expenses" element={<OrgERPExpenses />} />
              <Route path="/org/erp/invoices" element={<OrgERPInvoices />} />
              <Route path="/org/erp/vouchers" element={<OrgERPVouchers />} />
              <Route path="/org/erp/reports" element={<OrgERPReports />} />

              {/* Consumer / Staff (B2C) Routes */}
              <Route path="/consumer" element={<WalletDashboard />} />
              <Route path="/consumer/transfers" element={<TransfersPage />} />
              <Route path="/consumer/cards" element={<CardFundingPage />} />
              <Route path="/consumer/vas" element={<VasPage />} />
              <Route path="/consumer/kyc" element={<KycCompliancePage />} />
              <Route path="/consumer/work" element={<WorkDashboard />} />
              <Route path="/consumer/loans" element={<WorkLoansPage />} />
              <Route path="/consumer/savings" element={<WorkSavingsPage />} />
              <Route path="/consumer/thrift" element={<ThriftPage />} />

              {/* Catch-all */}
              <Route path="*" element={<RootRedirect />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ToastProvider>
  );
}
