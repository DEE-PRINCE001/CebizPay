/**
 * Centralized Application Route Constants
 */
export const ROUTES = {
  // Public
  HOME: '/',
  CAREERS: '/careers',
  CAREER_DETAIL: '/careers/:jobId',

  // Authentication
  LOGIN: '/login',
  REGISTER_PHONE: '/register',
  VERIFY_OTP: '/register/verify-otp',
  MFA_VERIFY: '/auth/mfa',
  REDEEM_INVITE: '/admin/invite/:token',
  FORGOT_PASSWORD: '/forgot-password',

  // Customer / Organization
  DASHBOARD: '/dashboard',
  WALLET: '/wallet',
  TRANSFERS: '/wallet/transfers',
  CARDS: '/wallet/cards',
  VAS: '/vas',
  PAYROLL: '/payroll',
  PAYROLL_BATCH: '/payroll/batch/:batchId',
  PAYROLL_VOUCHERS: '/payroll/vouchers',
  STAFF: '/staff',
  STAFF_INVITE: '/staff/invite',
  DEPARTMENTS: '/departments',
  SALARY_LEVELS: '/salary-levels',
  ROLES: '/roles',
  INVENTORY: '/inventory',
  INVENTORY_ITEMS: '/inventory/items',
  INVENTORY_CATEGORIES: '/inventory/categories',
  SERVICES: '/services',
  ORDERS: '/orders',
  SALES: '/sales',
  INVOICES: '/invoices',
  INVOICE_CREATE: '/invoices/create',
  INVOICE_DETAIL: '/invoices/:id',
  PURCHASES: '/purchases',
  SUPPLIERS: '/suppliers',
  CUSTOMERS: '/customers',
  EXPENSES: '/expenses',
  SAVINGS: '/savings',
  THRIFT: '/thrift',
  LOANS: '/loans',
  SETTINGS: '/settings',
  KYB_VERIFICATION: '/settings/kyb',
  INDIVIDUAL_KYC: '/settings/kyc',

  // SuperAdmin
  ADMIN_DASHBOARD: '/admin',
  ADMIN_AUDIT_LOGS: '/admin/audit-logs',
  ADMIN_COMPLIANCE: '/admin/compliance',
  ADMIN_FEES: '/admin/fees',
  ADMIN_RECONCILIATION: '/admin/reconciliation',
  ADMIN_THRIFT: '/admin/thrift',
  ADMIN_USERS: '/admin/users'
};
