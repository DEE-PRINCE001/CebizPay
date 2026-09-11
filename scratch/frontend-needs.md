# Backend Endpoints Needed for Individuals Module

This document tracks the administrative endpoints required by the frontend application for the **Individual** module that are **not yet available** on the live backend (`https://cebizpay.onrender.com/openapi/v1.json`).

> **Note on Existing Endpoints**:
> - `GET /api/v1/individuals/{id}/kyc-documents` is **already available** and verified.
> - `PATCH /api/v1/individuals/{id}/kyc-status` is **already available** and verified.
> - The endpoints below are **missing (currently returning 404 Not Found)** and are required for the Individual admin screens.

---

## 1. Platform Individuals Directory
Retrieves a paginated list of all onboarded individual users across the platform for administrative oversight.

- **Method**: `GET`
- **Route**: `/api/v1/admin/individuals`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search query matching individual's full name, email, or phone number.
  - `status` (`string` or `integer`, optional): Filter by lifecycle status (`Pending`, `Verified`, `Suspended`, `Rejected`).
  - `professionalStatus` (`string`, optional): Filter by employment classification (`Staff`, `Not-a-Staff`).
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Johnson Mile",
      "email": "Mile@gmail.com",
      "phoneNumber": "0815275927",
      "professionalStatus": "Staff",
      "companyName": "Cebis Company",
      "status": "Suspended",
      "avatarUrl": "https://storage.cebizpay.com/avatars/user_01.jpg",
      "createdAt": "2026-01-10T12:00:00Z"
    },
    {
      "id": "4bc96g75-6828-5673-c4gd-3d074g77bgb7",
      "name": "Mike Johnson",
      "email": "Mike@gmail.com",
      "phoneNumber": "0815275927",
      "professionalStatus": "Not-a-Staff",
      "companyName": "None",
      "status": "Verified",
      "avatarUrl": null,
      "createdAt": "2026-01-12T14:30:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 42,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 2. Individual Profile Details by ID
Retrieves full profile, employment, and submitted KYC credential details for an individual user.

- **Method**: `GET`
- **Route**: `/api/v1/admin/individuals/{id}`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Individual user unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Mike Johnson",
  "email": "Mike@gmail.com",
  "phoneNumber": "0815275927",
  "status": "Active",
  "professionalStatus": "Staff",
  "companyName": "Cebis Company",
  "photoUrl": "https://storage.cebizpay.com/photos/mike_johnson.jpg",
  "registeredAt": "2026-01-10T12:00:00Z",
  "credentials": [
    {
      "id": "doc-001",
      "title": "National Identity Card",
      "documentType": "NIN",
      "documentNumber": "12345678901",
      "fileUrl": "https://storage.cebizpay.com/docs/nin_card.pdf",
      "uploadedAt": "2026-01-10T12:15:00Z"
    }
  ]
}
```

---

## 3. Individual Transactions (Admin View)
Retrieves the paginated ledger transaction history for a specific individual.

- **Method**: `GET`
- **Route**: `/api/v1/admin/individuals/{id}/transactions`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Individual user unique identifier.
- **Query Parameters**:
  - `pageNumber` (`integer`, optional, default: `1`): The 1-based page number.
  - `pageSize` (`integer`, optional, default: `10`): Number of records per page.
  - `search` (`string`, optional): Search query matching counterparty, transaction ID, or account number.
  - `type` (`string`, optional): Filter by transaction type (`Send`, `Receives`).
  - `status` (`string`, optional): Filter by status (`Successfull`, `Pending`, `Reversed`, `Failed`).
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "tx-1029384756",
      "counterpartyName": "Johnson Mike",
      "counterpartyAvatarUrl": null,
      "amount": 25000.00,
      "transactionType": "Send",
      "receiverSenderId": "7817971681ID",
      "method": "Wallet ID",
      "accountOrWalletId": "156191667631",
      "dateTime": "2021-05-27T16:18:00Z",
      "status": "Successfull"
    },
    {
      "id": "tx-1029384757",
      "counterpartyName": "Johnson Mike",
      "counterpartyAvatarUrl": null,
      "amount": 10500.00,
      "transactionType": "Receives",
      "receiverSenderId": "7817971681ID",
      "method": "Bank Account",
      "accountOrWalletId": "156191667631",
      "dateTime": "2021-05-27T16:18:00Z",
      "status": "Pending"
    },
    {
      "id": "tx-1029384758",
      "counterpartyName": "Johnson Mike",
      "counterpartyAvatarUrl": null,
      "amount": 50000.00,
      "transactionType": "Send",
      "receiverSenderId": "7817971681ID",
      "method": "Wallet ID",
      "accountOrWalletId": "156191667631",
      "dateTime": "2021-05-27T16:18:00Z",
      "status": "Reversed"
    },
    {
      "id": "tx-1029384759",
      "counterpartyName": "Johnson Mike",
      "counterpartyAvatarUrl": null,
      "amount": 1200.00,
      "transactionType": "Receives",
      "receiverSenderId": "7817971681ID",
      "method": "Bank Account",
      "accountOrWalletId": "156191667631",
      "dateTime": "2021-05-27T16:18:00Z",
      "status": "Failed"
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

## 4. Individual Wallets Overview (Admin View)
Retrieves wallet account balances and ledger metadata for a specific individual.

- **Method**: `GET`
- **Route**: `/api/v1/admin/individuals/{id}/wallets`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Individual user unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "walletId": "WAL-89234710",
  "availableBalance": 450000.00,
  "ledgerBalance": 450000.00,
  "currency": "NGN",
  "tier": 2,
  "virtualAccountNumber": "0123456789",
  "bankName": "Wema Bank / CebizPay",
  "status": "Active"
}
```

---

## 5. Individual Savings Plans (Admin View)
Retrieves active and completed savings plans associated with a specific individual.

- **Method**: `GET`
- **Route**: `/api/v1/admin/individuals/{id}/savings`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Path Parameters**:
  - `id` (`uuid` or `string`, required): Individual user unique identifier.
- **Success Response (`200 OK`)**:
```json
{
  "items": [
    {
      "id": "sav-001",
      "name": "Target Savings - New Car",
      "targetAmount": 2000000.00,
      "currentAmount": 650000.00,
      "frequency": "Monthly",
      "interestRate": 12.5,
      "startDate": "2025-06-01T00:00:00Z",
      "maturityDate": "2026-06-01T00:00:00Z",
      "status": "Active"
    }
  ],
  "totalCount": 1
}
```

---

## 6. Export Individuals Directory
Exports the individual directory list to a downloadable CSV file.

- **Method**: `GET`
- **Route**: `/api/v1/admin/individuals/export`
- **Security**: Bearer JWT (Roles: `Admin`, `SuperAdmin`, `Auditor`)
- **Query Parameters**:
  - `search` (`string`, optional): Search query filter.
  - `status` (`string` or `integer`, optional): Lifecycle status filter.
- **Success Response (`200 OK`)**:
  - Content-Type: `text/csv; charset=utf-8`
  - Body: Binary CSV stream.
