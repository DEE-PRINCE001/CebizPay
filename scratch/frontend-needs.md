# Backend API Requirements: Organization Wallet & Settings Modules

This document outlines the API updates required by the frontend application for the **Organization Tenant Portal** (`/org/wallet` and `/org/settings`).

All routes are based on the live backend (`https://cebizpay.onrender.com/api/v1`).

---

## 1. Endpoints Requiring Schema Extensions

### 1.1 `GET /api/v1/org/wallet` — Cumulative Dashboard Metrics
* **Current Behavior**: Returns only `availableBalance` and `ledgerBalance` within `OrgWalletOverviewDto`.
* **Frontend Requirement**: The Organization Wallet dashboard (`WalletPage.png`) displays 4 primary summary cards:
  1. `Current Balance` (available via `availableBalance`)
  2. `Total Salary Paid`
  3. `Total Loan Fund`
  4. `Total Saving Money`
* **Requested Extension**: Add cumulative disbursement/fund totals to `OrgWalletOverviewDto` (similar to what is currently returned by `GET /api/v1/admin/organizations/{id}/wallet`).

#### Preferred Response Structure (`200 OK`)
```json
{
  "walletId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "organizationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "availableBalance": 238000909.00,
  "ledgerBalance": 238000909.00,
  "currency": "NGN",
  "status": "Active",
  "accountNumber": "0123456789",
  "accountName": "Cebis Tech",
  "bankName": "Wema Bank / CebizPay",
  "bankCode": "035",

  // --- NEW FIELDS NEEDED ---
  "totalSalaryPaid": 238000909.00,
  "totalLoanFund": 238000909.00,
  "totalSavingMoney": 238000909.00
}
```

---

### 1.2 `POST /api/v1/org/recruitment/jobs` & `JobPostingDto` — Banner & Application Process
* **Current Behavior**: `CreateJobPostingApiRequest` accepts text metadata (`title`, `description`, `employmentType`, `departmentId`, `workforceRoleId`, `salaryLevelId`, `location`, `requirements`, `responsibilities`, `applicationDeadline`).
* **Frontend Requirement**: The job creation UI (`CreateJobOffer.png`) includes:
  1. A promotional banner dropzone (JPEG/PNG image upload).
  2. An application method selector: **Send to mail** vs **Application form**.
* **Requested Extension**:
  * Add `bannerUrl` (`string`, optional, URI).
  * Add `applicationProcess` (`string`, optional, e.g., `"Email"` | `"PlatformForm"`).
  * Add `applicationEmail` (`string`, optional, email address when process is `"Email"`).
  * Reflect these fields in the returned `JobPostingDto`.

#### Preferred Request Body Extension (`CreateJobPostingApiRequest`)
```json
{
  "title": "Senior Frontend Engineer",
  "description": "Lead web platform frontend engineering...",
  "employmentType": "FullTime",
  "location": "Lagos, Nigeria (Hybrid)",
  "requirements": "5+ years React experience...",
  "responsibilities": "Ship scalable interfaces...",
  "applicationDeadline": "2026-10-31T23:59:59Z",
  "departmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "workforceRoleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "salaryLevelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",

  // --- NEW FIELDS NEEDED ---
  "bannerUrl": "https://res.cloudinary.com/cebizpay/image/upload/v1/banners/job-101.png",
  "applicationProcess": "Email",
  "applicationEmail": "careers@cebistech.com"
}
```

---

### 1.3 `POST /api/v1/announcements` & `AnnouncementDto` — Banner Image Support
* **Current Behavior**: `CreateAnnouncementRequest` accepts `scope`, `title`, `description`, `publishImmediately`.
* **Frontend Requirement**: The announcement creation UI (`createAnnouncement.png`) includes an image dropzone for banner graphics.
* **Requested Extension**:
  * Add `bannerUrl` (`string`, optional, URI) to `CreateAnnouncementRequest`.
  * Return `bannerUrl` (`string`, nullable) in `AnnouncementDto`.

#### Preferred Request Body Extension (`CreateAnnouncementRequest`)
```json
{
  "scope": "Workplace",
  "title": "Annual Company Retreat 2026",
  "description": "Details regarding accommodation and itinerary...",
  "publishImmediately": true,

  // --- NEW FIELD NEEDED ---
  "bannerUrl": "https://res.cloudinary.com/cebizpay/image/upload/v1/announcements/retreat.png"
}
```

---

## 2. Endpoints That Need to Be Created

### 2.1 `GET /api/v1/org/profile` — Tenant Corporate Profile & KYB Documents
* **Problem**: Currently, full corporate details (`email`, `phoneNumber`, `logoUrl`, and CAC registration document URLs) are only accessible via Platform Admin routes (`GET /api/v1/admin/organizations/{id}`). Tenant admins calling `/auth/me` only receive basic membership data (`companyName`, `role`, `status`) without contact details or verification documents needed for `SettingPage.png`.
* **Method**: `GET`
* **Route**: `/api/v1/org/profile`
* **Security**: Bearer JWT (Roles: `OrganizationAdmin`, `OrganizationOwner`)
* **Tenant Isolation**: Resolves the active organization from the caller's JWT token or `X-Organization-Id` header.

#### Preferred Success Response (`200 OK`)
```json
{
  "organizationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Cebis Technologies",
  "email": "support@cebistech.com",
  "phoneNumber": "+234 801 234 5678",
  "address": "Victoria Island, Lagos, Nigeria",
  "category": "Technology",
  "status": "Active",
  "logoUrl": "https://res.cloudinary.com/cebizpay/image/upload/v1/logos/cebis.png",
  "cacNumber": "RC1234567",
  "cacCertificateUrl": "https://res.cloudinary.com/cebizpay/raw/upload/v1/kyb/CAC-RC1234567-Certificate.pdf",
  "registeredAtUtc": "2026-01-15T10:00:00Z"
}
```

---

### 2.2 `GET /api/v1/org/wallet/transactions/export` — Export Wallet Transactions to CSV
* **Problem**: The transaction history ledger on the Wallet page (`WalletPage.png`) includes an **Export CSV** action. While the platform admin has export endpoints, the tenant organization wallet currently does not have an export route.
* **Method**: `GET`
* **Route**: `/api/v1/org/wallet/transactions/export`
* **Security**: Bearer JWT (Roles: `OrganizationAdmin`, `OrganizationOwner`, `OrganizationFinance`)
* **Query Parameters**:
  * `search` (`string`, optional): Search query filtering reference, counterparty, or description.
  * `status` (`string`, optional): Filter by status (`Successfull`, `Pending`, `Failed`).
  * `type` (`string`, optional): Filter by transaction type (`Transfer`, `Funding`, `Payroll`).
  * `fromUtc` (`date-time`, optional): Start boundary.
  * `toUtc` (`date-time`, optional): End boundary.
* **Success Response (`200 OK`)**:
  * `Content-Type`: `text/csv; charset=utf-8`
  * `Content-Disposition`: `attachment; filename="org-wallet-transactions.csv"`
  * Body: Binary or text CSV stream.

---

## Summary Matrix for Backend Team

| Target Endpoint | Action Required | Priority | Impact on Frontend UI |
| :--- | :---: | :---: | :--- |
| `GET /api/v1/org/wallet` | **Extend DTO** | **High** | Provides live totals for `Total Salary Paid`, `Total Loan Fund`, and `Total Saving Money` cards. |
| `GET /api/v1/org/profile` | **Create Route** | **High** | Supplies corporate contact email, phone, logo, and clickable CAC document URL for `/org/settings`. |
| `POST /api/v1/org/recruitment/jobs` | **Extend Request/DTO** | **Medium** | Enables persisting job banner URL and application delivery method (*Send to mail* vs *Application form*). |
| `POST /api/v1/announcements` | **Extend Request/DTO** | **Medium** | Enables persisting announcement graphic banner URL. |
| `GET /api/v1/org/wallet/transactions/export` | **Create Route** | **Low** | Streams server-generated CSV transaction export. *(Can fallback to client-side CSV parsing if needed).* |
