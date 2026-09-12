# Backend Endpoints Needed for Wallet Management Module

This document tracks the administrative endpoints required by the frontend application for the **Wallet Management** module (covering Organization Wallets, Organization Wallet Details, and Individual Wallets) that are **currently not available (returning 404 Not Found)** on the live backend (`https://cebizpay.onrender.com`).

> **Note on Existing Verified Endpoints**:
> - `GET /api/v1/admin/organizations` is available (returns basic organization list).
> - `GET /api/v1/admin/organizations/{id}` is available (returns single organization info).
> - `GET /api/v1/admin/organizations/{id}/payroll-analytics` is available (returns aggregated payroll analytics).
> - `GET /api/v1/admin/individuals` is available (returns basic individual list).
> - `GET /api/v1/admin/individuals/{id}/wallets` is available (returns single individual wallet).
> - The endpoints below are **missing** and required for the wallet screens.

---

## 1. Platform Organization Wallets Directory
Retrieves a paginated list of all corporate organization wallets with their current ledger balances, cumulative salary disbursements, and total loan disbursements across the platform.

- **Method**: `GET`
- **Route**: `/api/v1/admin/wallets/organizations`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search query matching organization name or registration number.
  - `status` (`string`, optional): Filter by lifecycle or wallet status (`Active`, `Suspended`, `Verified`).
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "0789e8fe-c8d9-43c9-aef0-598c92a27704",
      "name": "Cebis Tech",
      "logoUrl": "https://storage.cebizpay.com/logos/cebis.png",
      "currentBalance": 238000909.00,
      "totalSalaryPaid": 238000909.00,
      "totalLoanPaid": 238000909.00,
      "currency": "NGN",
      "status": "Active"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 45,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 2. Export Organization Wallets Directory
Exports the organization wallets directory list to a downloadable CSV stream.

- **Method**: `GET`
- **Route**: `/api/v1/admin/wallets/organizations/export`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `search` (`string`, optional): Search query filter.
  - `status` (`string`, optional): Status filter.
- **Success Response (`200 OK`)**:
  - Content-Type: `text/csv; charset=utf-8`
  - Body: Binary CSV stream.

---

## 3. Organization Wallet Overview by ID
Retrieves specific corporate wallet metrics including current ledger balance, cumulative salary disbursement, and total corporate loan funds.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}/wallet`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Organization unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "organizationId": "0789e8fe-c8d9-43c9-aef0-598c92a27704",
  "walletId": "WAL-ORG-0789E8FE",
  "currency": "NGN",
  "currentBalance": 238000909.00,
  "totalSalaryPaid": 238000909.00,
  "totalLoanFund": 238000909.00,
  "virtualAccountNumber": "0123456789",
  "bankName": "Wema Bank / CebizPay",
  "status": "Active"
}
```

---

## 4. Organization Salaries / Payroll Disbursements (Admin View)
Retrieves a paginated list of salary disbursement line items executed by the corporate entity.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}/salaries`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Organization unique identifier.
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search by transaction ID, employee name, or wallet ID.
  - `month` (`string`, optional): Filter by payroll month (e.g., `January`, `May`).
  - `status` (`string`, optional): Filter by status (`Successfull`, `Pending`, `Failed`).
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "sal-1029384756",
      "amount": 34000.00,
      "transactionId": "2619861816688",
      "method": "Wallet ID",
      "accountOrWalletId": "156191667631",
      "month": "January",
      "dateTime": "2021-05-27T16:18:00Z",
      "status": "Successfull"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 130,
  "totalPages": 13,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 5. Export Organization Salaries List
Exports the salary disbursements of a specific organization to a downloadable CSV stream.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}/salaries/export`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Organization unique identifier.
- **Success Response (`200 OK`)**:
  - Content-Type: `text/csv; charset=utf-8`
  - Body: Binary CSV stream.

---

## 6. Organization Savings Plans (Admin View)
Retrieves active target and fixed saving plans configured for an organization.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}/savings`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Organization unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "sav-org-001",
      "name": "Corporate Reserve Fund",
      "targetAmount": 10000000.00,
      "currentAmount": 4500000.00,
      "frequency": "Monthly",
      "interestRate": 12.0,
      "startDate": "2025-01-01T00:00:00Z",
      "maturityDate": "2026-01-01T00:00:00Z",
      "status": "Active"
    }
  ],
  "totalCount": 1
}
```

---

## 7. Platform Individual Wallets Directory
Retrieves a paginated list of all individual user wallets with current available balances and total outstanding repayable loan amounts.

- **Method**: `GET`
- **Route**: `/api/v1/admin/wallets/individuals`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search query matching individual's full name, email, or wallet ID.
  - `status` (`string`, optional): Filter by status (`Active`, `Suspended`, `Pending`).
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "4ae02dbe-3a60-4854-b69d-bcf61ac6323b",
      "name": "Adejumo micheal",
      "avatarUrl": "https://api.dicebear.com/7.x/initials/svg?seed=AM",
      "currentBalance": 238000909.00,
      "loanRepayable": 238000909.00,
      "currency": "NGN",
      "status": "Active"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 130,
  "totalPages": 13,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 8. Export Individual Wallets Directory
Exports the individual wallets list to a downloadable CSV stream.

- **Method**: `GET`
- **Route**: `/api/v1/admin/wallets/individuals/export`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `search` (`string`, optional): Search query filter.
  - `status` (`string`, optional): Status filter.
- **Success Response (`200 OK`)**:
  - Content-Type: `text/csv; charset=utf-8`
  - Body: Binary CSV stream.
