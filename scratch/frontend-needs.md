# Backend Endpoints Needed for Organizations Module

This document tracks endpoints required by the frontend application that are not yet available or fully exposed in the backend OpenAPI specification (`https://cebizpay.onrender.com/openapi/v1.json`).

---

## 1. Platform Organizations Directory
Retrieves a paginated list of all onboarded organizations across the platform for administrative oversight.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search query matching organization name, email, or address.
  - `status` (`string` or `integer`, optional): Filter by lifecycle status (`Pending`, `Verified`, `Suspended`, `Rejected`).
  - `category` (`string`, optional): Filter by industry category (e.g. `Technology`, `Finance`).
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Cebis Tech",
      "category": "Technology",
      "email": "cebistech@gmail.com",
      "address": "Abuja Obanikoro.......",
      "status": "Pending",
      "staffCount": 56,
      "logoUrl": null,
      "createdAt": "2026-01-15T09:30:00Z"
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

## 2. Organization Details by ID
Retrieves full operational and KYB profile details for a specific organization.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid`, required): Organization unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Cebis Tech",
  "category": "Technology",
  "email": "cebistech@gmail.com",
  "address": "Abuja Obanikoro.......",
  "status": "Pending",
  "staffCount": 56,
  "logoUrl": null,
  "photoUrl": null,
  "registeredAt": "2026-01-10T12:00:00Z",
  "credentials": [
    {
      "id": "doc-001",
      "title": "Corporative Association Community",
      "documentType": "CAC_CERTIFICATE",
      "fileUrl": "https://storage.cebizpay.com/docs/cac_cebis_tech.pdf",
      "uploadedAt": "2026-01-10T12:15:00Z"
    },
    {
      "id": "doc-002",
      "title": "Corporative Association Community",
      "documentType": "MEMORANDUM_OF_ASSOCIATION",
      "fileUrl": "https://storage.cebizpay.com/docs/moa_cebis_tech.pdf",
      "uploadedAt": "2026-01-10T12:15:00Z"
    },
    {
      "id": "doc-003",
      "title": "Corporative Association Community",
      "documentType": "TAX_CLEARANCE",
      "fileUrl": "https://storage.cebizpay.com/docs/tax_cebis_tech.pdf",
      "uploadedAt": "2026-01-10T12:15:00Z"
    }
  ]
}
```

---

## 3. Organization Staff Roster (Platform Admin Scope)
Retrieves staff members associated with a specific organization when viewed by a platform administrator.
*(Note: `/api/v1/org/staff` currently requires tenant organization context `X-Organization-Id`; this endpoint allows platform admins to view staff by organization ID).*

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}/staff`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid`, required): Organization unique identifier.
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search staff name, wallet ID, or email.
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "staff-001",
      "name": "Johnson Mike",
      "walletId": "781797168ID",
      "bankAccount": "02826893 AC",
      "email": "Mile@gmail.com",
      "monthlySalary": "34,9713",
      "status": "Verified",
      "avatarUrl": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 65,
  "totalPages": 7,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 4. Organization Submitted Credentials & Documents
Retrieves or downloads the verification document files (e.g. CAC certificate, Memorandum of Association, Tax Clearance) submitted during KYB onboarding.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/{id}/documents`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid`, required): Organization unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "organizationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "documents": [
    {
      "id": "doc-001",
      "title": "Corporative Association Community",
      "documentType": "CAC_CERTIFICATE",
      "fileUrl": "https://cebizpay-storage.s3.amazonaws.com/kyb/cac_certificate.pdf",
      "fileSizeBytes": 1048576,
      "uploadedAt": "2026-01-10T12:15:00Z"
    }
  ]
}
```

---

## 5. Server-Side Streaming CSV Export
Allows exporting filtered organization datasets directly as a downloadable CSV or Excel file.

- **Method**: `GET`
- **Route**: `/api/v1/admin/organizations/export`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `search` (`string`, optional)
  - `status` (`string` or `integer`, optional)
  - `format` (`string`, optional, default: `csv`): `csv` or `xlsx`.
- **Response (`200 OK`)**:
  - `Content-Type`: `text/csv`
  - `Content-Disposition`: `attachment; filename="organizations_export.csv"`

---

## 6. Organization Status Enum Specification (Existing Live Endpoint)
For reference with the live endpoint `PATCH /api/v1/organizations/{id}/status`:
- **Payload**:
```json
{
  "status": 2,
  "reason": "Approved by administrator"
}
```
- **Enum Integer Mapping (`OrganizationStatus` - 1-based enum in backend Domain entity)**:
  - `1`: `Pending` (Under KYB review)
  - `2`: `Verified` / `Active` (Approved and fully operational)
  - `3`: `Rejected` (Verification rejected)
  - `4`: `Suspended` (Temporarily restricted by administrator)

- **Domain Lifecycle Transition Rules (`Organization.TransitionStatus`)**:
  - `Pending (1)` $\rightarrow$ Can transition to `Verified (2)` or `Rejected (3)`
  - `Verified (2)` $\rightarrow$ Can transition to `Suspended (4)`
  - `Suspended (4)` $\rightarrow$ Can transition to `Verified (2)` (Re-activate)
  - `Rejected (3)` $\rightarrow$ Can transition to `Pending (1)` (Review)
  *Note: Attempting transitions outside these rules (e.g. sending 0 or jumping from Pending to Suspended directly) causes a `500 System.InvalidOperationException` on the backend.*

