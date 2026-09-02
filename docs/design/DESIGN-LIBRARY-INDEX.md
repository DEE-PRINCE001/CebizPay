# CebizPay Visual Design Library Index

## Overview & Catalogue Architecture

The CebizPay visual design library consists of **479 visual assets and screen designs** located in `frontend/design-library/`.

This document serves as the **Authoritative Visual Design Index and Retrieval Map** for frontend engineering. Rather than loading all 479 images into developer context simultaneously, developers and agents should treat the design directory as a **virtual visual asset library** and query this index to locate specific visual references when building features.

---

## 1. Design Library Structure & Summary Statistics

| Dimension / Metric | Count | Details |
| :--- | :--- | :--- |
| **Total Visual Assets** | 479 | All PNG format assets residing in `frontend/design-library/` |
| **Desktop Full Pages (>= 1000px)** | 118 | High-fidelity desktop viewports (1440x900 / 1440x1080 / 1440x1164) |
| **Modals, Drawers & Forms (300px - 900px)** | 287 | Dialogs, slide-over sheets, form cards, and confirmation dialogs |
| **Mobile Viewports (< 500px w / >= 600px h)** | 6 | Dedicated mobile responsive screens (iPhone 14 Pro viewports) |
| **UI Atoms & Controls (< 250px)** | 68 | Iconography, button states, export menus, dropdowns, and badges |

---

## 2. Functional Screen Families & Workflows

To facilitate rapid retrieval, the visual assets are categorized into 18 core functional screen families:

### Family 1: Executive & Operational Dashboards
- **Core Screens**: `Dashboard.png`, `Dashboard-1.png`, `Good Morning.png`, `Hello Tayo.png`
- **Role**: Top-level executive overview, user greeting, high-level metrics (Organisations, Individuals, Active Users, Pending KYB/KYC, Savings Plans), wallet balance display, and recent announcements.

### Family 2: Organization & Corporate Administration
- **Core Screens**: `Organization-1.png`, `Organization 2.png` - `5.png`, `Mnage Company.png`, `Profile oganiza.png` - `4.png`, `Create company.png`, `Edit company.png`, `Organizqation.png`
- **Role**: Corporate profile management, organization KYB status, business details, registration verification.

### Family 3: Retail / Individual Banking & KYC
- **Core Screens**: `Individual-1.png`, `Individual Wallet.png`, `Individual Savings Plan.png`, `Individual Request.png`, `Individual verified.png` (1-9)
- **Role**: Retail consumer profiles, individual wallet balances, savings overview, KYC submission status and document validation states.

### Family 4: Wallet, Transfers & Funding
- **Core Screens**: `Organisations Wallet.png`, `Wallet Org..png`, `Transfer Payment MODE.png` (1-2), `Transfer Fund.png` (1), `Transfer to bank.png` (1-5), `Withdraw Via Card.png` (1-2), `Withdraw Via Merchant.png` (1-8), `Withdraw Via.png`, `Add card.png`
- **Role**: Funding overview, Peer-to-Peer transfer modals, NIP bank transfer forms, account resolver feedback, card funding, merchant withdrawal workflows.

### Family 5: Saved Cards & Payment Methods
- **Core Screens**: `Card management.png`, `Add card.png`, `iPhone 14 Pro - 98.png` (Card selection radio list), `Payment Option.png`, `Payment mode.png`
- **Role**: Saved debit/credit cards list, default card designation, zero-auth card tokenization.

### Family 6: Corporate Payroll & Employee Payslips
- **Core Screens**: `Payroll(Schedule.png` (1-3), `Payroll(History.png` (1), `Payroll(Analytics.png` (1), `All Payroll.png`, `Pay by level.png` (1), `Pay all.png`, `Pay p.png`, `Payslip.png`
- **Role**: Scheduling payroll runs, dry-run payroll preview calculations, batch history, breakdown by salary level, employee payslip generation.

### Family 7: Corporate Payment Vouchers
- **Core Screens**: `Voucher.png`, `Create voucher.png`, `Edit voucher.png` (1), `view voucher.png`
- **Role**: Payment voucher issuance, voucher review, settlement status, voucher printing/export.

### Family 8: Workforce, Staff & HR Management
- **Core Screens**: `Staff.png`, `ALL Staff.png` (1), `All staff(1).png`, `All Staff(2).png`, `MemberProfile.png`, `Team Profile.png` (1), `Add New Member.png`, `Add member.png`, `Invite users.png`, `Invite code.png`, `Invited succ.png`
- **Role**: Staff roster, employee profile view, invitation flow with invite code / email, suspension/activation, department assignment.

### Family 9: Organizational Structure & Departments
- **Core Screens**: `Departments.png`, `Manage Depts.png`, `Create Depts.png`, `Delete Dept.png` (1-2), `Designers.png`, `Information Technology.png`
- **Role**: Department creation modal, department list, staff per department, deletion confirmation dialogs.

### Family 10: Compensation & Salary Levels
- **Core Screens**: `All lev.png` (1), `Create Level.png` (1), `Manage L.png`
- **Role**: Salary grade hierarchy, base pay bracket setup, allowance/deduction definitions.

### Family 11: Access Control, Roles & Permissions
- **Core Screens**: `Roles.png`, `Add New Role.png`, `Grant Permission.png` (1-5), `add New Admin.png` (1)
- **Role**: Role definition modal, granular permission checkboxes, admin delegation, administrative user creation.

### Family 12: Invoicing & Accounts Receivable
- **Core Screens**: `Invoice generator.png`, `Invoice settings.png`, `Invoice settings(Account).png`, `Invoice settings(Contact).png`, `Invoice settings(tags).png`, `view invoice Order.png` (1), `view invoice p.png` (1), `view invoice sa.png`, `Send Invoice.png`
- **Role**: Invoice builder, invoice tax/tag/contact settings, professional invoice PDF viewer, invoice status tracking (Open, Closed, Overdue).

### Family 13: ERP: Inventory, Catalog & Valuation
- **Core Screens**: `Inventory.png`, `Real Inventory.png`, `items inventory.png` (1-6), `items details.png` (1), `items Categories.png`, `Add Item Categores.png` (1-2), `add items.png` (2), `Items.png` (1)
- **Role**: Stock level tracking, SKU/pricing management, item categorization, stock movement audit, inventory valuation method selector (FIFO/WAC).

### Family 14: ERP: Sales, Orders & Procurement
- **Core Screens**: `order inventory.png` (1), `oredr inventory.png` (1-2), `order details.png` (1), `Order Customer History.png`, `Add ORDER.png` (1-3), `sales inventory.png` (1-3), `sales details.png` (1), `Add Sales 2.png` (3), `PURCHASE.png` (1-3), `purchase inventory.png`, `purchase details.png`, `Add Purchase.png` (1-2), `Expenses inventory.png`
- **Role**: Sales order registry, checkout/order creation, purchase order procurement, daily/weekly/monthly/annual P&L analytics.

### Family 15: ERP: Services & Suppliers
- **Core Screens**: `ServicesCategories.png`, `Add service.png` (2), `service inventory.png` (1-3), `service details.png` (1), `Service rendered add.png`, `Service Boughtadd.png`, `supplier's inventory.png` (1), `suppliers details.png` (1), `ADD supplier.png` (2-3)
- **Role**: Service catalog definitions, supplier directory, vendor contact information, supply history.

### Family 16: Corporate Loans & Staff Credit
- **Core Screens**: `Loans View.png` (1-2), `Loan Request.png`, `Create Loan.png`, `Create loan(1).png`, `Create plan.png`
- **Role**: Loan plan configuration (interest rates, tenor, max limit), employee loan application form, repayment schedule viewer.

### Family 17: Savings Schemes & Thrift (Ajo / Esusu)
- **Core Screens**: `Organisations Savings Plan.png`, `Individual Savings Plan.png`, `Saving Plans.png` (1-3), `Manage Groups.png`, `TLF.png`, `TSM.png`, `TSP.png`
- **Role**: Target savings goals, locked fixed deposits, rotational thrift group management (Ajo/Esusu), position selection.

### Family 18: Recruitment & ATS Pipeline
- **Core Screens**: `Job Offer.png` (1-2), `Job Pub.png`, `Applicants.png` (1-4)
- **Role**: Job vacancy creation, public job spec publication, candidate pipeline stage tracking (Applied, Screened, Interviewed, Offered).

---

## 3. UI Component & Interaction Primitives

The design library includes specialized micro-components and interaction states:

### Table Action & Export Dropdowns
- `Download.png`, `Download-1.png` through `Download-34.png` (138x100px)
- **Usage**: Standard popover menu offering format selections (CSV, XLSX, PDF) when the user clicks **Export** on any data table.

### Filter Dropdown Panels
- `FILTER.png`, `FILTER-1.png` through `FILTER-8.png`, `FILTER PAY.png` (445x561px)
- **Usage**: Multi-criteria filtering popover (Date range picker, Category filter, Status chips, Payment mode checkboxes).

### Confirmation Dialogs
- `Delete Subject.png`, `Delete Subject-1.png` through `Delete Subject-18.png` (358x331px)
- **Usage**: Reusable modal for dangerous/destructive actions ("Are you sure you want to delete this [Department / Role / Item / Member]?").

### Operation Feedback & Success Modals
- `done.png`, `done-1.png` through `done-31.png` (438x324px)
- `Successful-17.png`, `Successful-18.png` (240x208px)
- `Subject deleted-17.png`, `Subject deleted-18.png` (358x267px)
- **Usage**: Standard post-action celebration modal with success checkmark icon, descriptive message, and "Done" / "Proceed" primary button.

### Mobile & Responsive Viewport References
- `iPhone 14 Pro - 91.png`, `97.png`, `98.png`, `99.png`, `101.png`, `102.png` (393px width)
- **Usage**: Mobile payment method selection drawer, saved card radio cards, and mobile bottom sheet interaction models.

---

## 4. Complete Design Library Inventory Table

The following table provides the complete searchable index of all 479 visual assets in `frontend/design-library/`:

| ID | File Name | Category / Area | Format / Dimensions | Core Purpose / UI Representation | Related Screen Family |
| :--- | :--- | :--- | :--- | :--- | :--- |
| D001 | `37.png` | Platform Utilities & Views | Icon / Control / Badge (155x155) | Supporting UI view or dialog component | General Navigation |
| D002 | `ADD supplier 2.png` | ERP: Supplier Management | Screen (526x768) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D003 | `ADD supplier 3.png` | ERP: Supplier Management | Screen (541x768) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D004 | `ADD supplier.png` | ERP: Supplier Management | Screen (530x768) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D005 | `ALL Staff-1.png` | Staff & Workforce HR | Screen (659x713) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D006 | `ALL Staff.png` | Staff & Workforce HR | Screen (659x905) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D007 | `Account.png` | Platform Utilities & Views | Modal / Popover / Widget (500x507) | Supporting UI view or dialog component | General Navigation |
| D008 | `Add Item Categores-1.png` | ERP: Inventory & Catalog | Modal / Popover / Widget (498x386) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D009 | `Add Item Categores-2.png` | ERP: Inventory & Catalog | Modal / Popover / Widget (498x386) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D010 | `Add Item Categores.png` | ERP: Inventory & Catalog | Modal / Popover / Widget (498x386) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D011 | `Add New Member.png` | Staff & Workforce HR | Screen (886x821) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D012 | `Add New Role.png` | Access Control & Roles | Modal / Popover / Widget (472x547) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D013 | `Add ORDER 2.png` | ERP: Orders Management | Mobile Viewport (498x735) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D014 | `Add ORDER 3.png` | ERP: Orders Management | Mobile Viewport (498x735) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D015 | `Add ORDER.png` | ERP: Orders Management | Mobile Viewport (498x735) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D016 | `Add Order(1).png` | ERP: Orders Management | Screen (500x646) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D017 | `Add Purchase 2.png` | ERP: Purchases & Procurement | Screen (500x752) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D018 | `Add Purchase-1.png` | ERP: Purchases & Procurement | Modal / Popover / Widget (500x463) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D019 | `Add Purchase.png` | ERP: Purchases & Procurement | Screen (500x752) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D020 | `Add Sales 2.png` | ERP: Sales & Orders | Screen (500x628) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D021 | `Add Sales 3.png` | ERP: Sales & Orders | Screen (500x646) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D022 | `Add card.png` | Card Management | Modal / Popover / Widget (472x593) | Saved cards, tokenization, card funding, set default funding card | Wallet, Funding |
| D023 | `Add customer-1.png` | CRM: Customer Management | Screen (500x752) | Customer accounts, purchase history, outstanding receivables, and contact details | Sales, Invoices, Receipts, Orders |
| D024 | `Add customer.png` | CRM: Customer Management | Screen (500x752) | Customer accounts, purchase history, outstanding receivables, and contact details | Sales, Invoices, Receipts, Orders |
| D025 | `Add member.png` | Staff & Workforce HR | Modal / Popover / Widget (472x543) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D026 | `Add service 2.png` | ERP: Services Catalog | Modal / Popover / Widget (499x528) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D027 | `Add service.png` | ERP: Services Catalog | Modal / Popover / Widget (499x528) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D028 | `All Payroll.png` | Corporate Payroll | Modal / Popover / Widget (472x475) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D029 | `All Staff(2).png` | Staff & Workforce HR | Modal / Popover / Widget (472x475) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D030 | `All lev-1.png` | Organization Compensation | Modal / Popover / Widget (472x475) | Define salary grades, levels, compensation brackets and benefits | Departments, Staff, Payroll |
| D031 | `All lev.png` | Organization Compensation | Modal / Popover / Widget (472x292) | Define salary grades, levels, compensation brackets and benefits | Departments, Staff, Payroll |
| D032 | `All staff(1).png` | Staff & Workforce HR | Modal / Popover / Widget (472x292) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D033 | `Announcement.png` | Platform Utilities & Views | Screen (659x880) | Supporting UI view or dialog component | General Navigation |
| D034 | `App Pro.png` | Platform Utilities & Views | Modal / Popover / Widget (472x375) | Supporting UI view or dialog component | General Navigation |
| D035 | `Applicants-1.png` | Recruitment & ATS | Screen (659x729) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D036 | `Applicants-2.png` | Recruitment & ATS | Screen (659x729) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D037 | `Applicants-3.png` | Recruitment & ATS | Screen (659x785) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D038 | `Applicants-4.png` | Recruitment & ATS | Screen (659x1042) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D039 | `Applicants.png` | Recruitment & ATS | Screen (659x729) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D040 | `Availability.png` | Platform Utilities & Views | Icon / Control / Badge (232x152) | Supporting UI view or dialog component | General Navigation |
| D041 | `By Personal.png` | Platform Utilities & Views | Modal / Popover / Widget (472x394) | Supporting UI view or dialog component | General Navigation |
| D042 | `CEBIZ 2.png` | Platform Utilities & Views | Icon / Control / Badge (150x146) | Supporting UI view or dialog component | General Navigation |
| D043 | `Card management.png` | Card Management | Screen (659x640) | Saved cards, tokenization, card funding, set default funding card | Wallet, Funding |
| D044 | `Categories-1.png` | Platform Utilities & Views | Icon / Control / Badge (226x110) | Supporting UI view or dialog component | General Navigation |
| D045 | `Categories-2.png` | Platform Utilities & Views | Modal / Popover / Widget (286x521) | Supporting UI view or dialog component | General Navigation |
| D046 | `Categories-3.png` | Platform Utilities & Views | Icon / Control / Badge (226x110) | Supporting UI view or dialog component | General Navigation |
| D047 | `Categories.png` | Platform Utilities & Views | Modal / Popover / Widget (286x548) | Supporting UI view or dialog component | General Navigation |
| D048 | `Component 118.png` | Design System & UI Components | Icon / Control / Badge (82x84) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D049 | `Component 253.png` | Design System & UI Components | Icon / Control / Badge (115x43) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D050 | `Component 256.png` | Design System & UI Components | Icon / Control / Badge (115x46) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D051 | `Component 259.png` | Design System & UI Components | Desktop Full Page (2926x1412) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D052 | `Component 261.png` | Design System & UI Components | Desktop Full Page (2907x1476) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D053 | `Component 264.png` | Design System & UI Components | Icon / Control / Badge (109x43) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D054 | `Component 265.png` | Design System & UI Components | Icon / Control / Badge (115x43) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D055 | `Component 268.png` | Design System & UI Components | Desktop Full Page (1412x1345) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D056 | `Component 269.png` | Design System & UI Components | Icon / Control / Badge (120x73) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D057 | `Component 274.png` | Design System & UI Components | Desktop Full Page (1412x2847) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D058 | `Component 276.png` | Design System & UI Components | Icon / Control / Badge (178x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D059 | `Component 277.png` | Design System & UI Components | Icon / Control / Badge (139x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D060 | `Component 278.png` | Design System & UI Components | Icon / Control / Badge (145x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D061 | `Component 279.png` | Design System & UI Components | Icon / Control / Badge (147x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D062 | `Component 280.png` | Design System & UI Components | Icon / Control / Badge (149x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D063 | `Component 281.png` | Design System & UI Components | Icon / Control / Badge (178x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D064 | `Component 282.png` | Design System & UI Components | Icon / Control / Badge (198x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D065 | `Component 283.png` | Design System & UI Components | Icon / Control / Badge (164x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D066 | `Component 284.png` | Design System & UI Components | Icon / Control / Badge (163x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D067 | `Component 285.png` | Design System & UI Components | Icon / Control / Badge (193x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D068 | `Component 286.png` | Design System & UI Components | Desktop Full Page (1372x618) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D069 | `Component 287.png` | Design System & UI Components | Desktop Full Page (1412x2847) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D070 | `Component 288.png` | Design System & UI Components | Icon / Control / Badge (168x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D071 | `Component 289.png` | Design System & UI Components | Icon / Control / Badge (168x183) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D072 | `Contact.png` | Platform Utilities & Views | Modal / Popover / Widget (500x387) | Supporting UI view or dialog component | General Navigation |
| D073 | `Create Annoucement.png` | Platform Utilities & Views | Screen (659x690) | Supporting UI view or dialog component | General Navigation |
| D074 | `Create Depts.png` | Organization Structure | Modal / Popover / Widget (472x593) | Create, update, and organize company departments | Staff, Roles, Salary Levels |
| D075 | `Create Form.png` | Platform Utilities & Views | Modal / Popover / Widget (472x375) | Supporting UI view or dialog component | General Navigation |
| D076 | `Create Level-1.png` | Organization Compensation | Modal / Popover / Widget (472x593) | Define salary grades, levels, compensation brackets and benefits | Departments, Staff, Payroll |
| D077 | `Create Level.png` | Organization Compensation | Mobile Viewport (472x763) | Define salary grades, levels, compensation brackets and benefits | Departments, Staff, Payroll |
| D078 | `Create Loan.png` | Lending & Credit | Screen (659x1040) | Corporate loan plans, employee loan requests, repayment tracking and approval | Payroll, Wallet, Staff |
| D079 | `Create company.png` | Organization & Settings | Modal / Popover / Widget (579x389) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D080 | `Create loan(1).png` | Lending & Credit | Modal / Popover / Widget (472x292) | Corporate loan plans, employee loan requests, repayment tracking and approval | Payroll, Wallet, Staff |
| D081 | `Create plan.png` | Platform Utilities & Views | Modal / Popover / Widget (472x292) | Supporting UI view or dialog component | General Navigation |
| D082 | `Create voucher.png` | Corporate Vouchers | Screen (579x1212) | Issue company vouchers, view voucher details, manage settlement and approval | Payroll, Wallet, Expenses |
| D083 | `Currency.png` | Platform Utilities & Views | Icon / Control / Badge (103x156) | Supporting UI view or dialog component | General Navigation |
| D084 | `Dashboard-1.png` | Executive Dashboards | Desktop Full Page (1440x998) | Aggregated financial metrics, active user stats, wallet balance, recent announcements and shortcuts | Wallet, Organizations, Individuals, Analytics |
| D085 | `Dashboard.png` | Executive Dashboards | Desktop Full Page (1440x1084) | Aggregated financial metrics, active user stats, wallet balance, recent announcements and shortcuts | Wallet, Organizations, Individuals, Analytics |
| D086 | `Date Picker.png` | Platform Utilities & Views | Modal / Popover / Widget (389x345) | Supporting UI view or dialog component | General Navigation |
| D087 | `Delete Dept-1.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (472x292) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D088 | `Delete Dept-2.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (472x292) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D089 | `Delete Dept.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (472x292) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D090 | `Delete Subject-1.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D091 | `Delete Subject-10.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D092 | `Delete Subject-11.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D093 | `Delete Subject-12.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D094 | `Delete Subject-13.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D095 | `Delete Subject-14.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D096 | `Delete Subject-15.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D097 | `Delete Subject-16.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D098 | `Delete Subject-17.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D099 | `Delete Subject-18.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D100 | `Delete Subject-2.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D101 | `Delete Subject-3.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D102 | `Delete Subject-4.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D103 | `Delete Subject-5.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D104 | `Delete Subject-6.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D105 | `Delete Subject-7.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D106 | `Delete Subject-8.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D107 | `Delete Subject-9.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D108 | `Delete Subject.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x331) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D109 | `Departments.png` | Organization Structure | Screen (659x671) | Create, update, and organize company departments | Staff, Roles, Salary Levels |
| D110 | `Designers.png` | Platform Utilities & Views | Screen (659x905) | Supporting UI view or dialog component | General Navigation |
| D111 | `Download-1.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D112 | `Download-10.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D113 | `Download-11.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D114 | `Download-12.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D115 | `Download-13.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D116 | `Download-14.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D117 | `Download-15.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D118 | `Download-16.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D119 | `Download-17.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D120 | `Download-18.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D121 | `Download-19.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D122 | `Download-2.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D123 | `Download-20.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D124 | `Download-21.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D125 | `Download-22.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D126 | `Download-23.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D127 | `Download-24.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D128 | `Download-25.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D129 | `Download-26.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D130 | `Download-27.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D131 | `Download-28.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D132 | `Download-29.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D133 | `Download-3.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D134 | `Download-30.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D135 | `Download-31.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D136 | `Download-32.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D137 | `Download-33.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D138 | `Download-34.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D139 | `Download-4.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D140 | `Download-5.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D141 | `Download-6.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D142 | `Download-7.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D143 | `Download-8.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D144 | `Download-9.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D145 | `Download.png` | Data Export & Actions | Icon / Control / Badge (138x100) | Dropdown/modal popup to trigger CSV, Excel, or PDF document exports | Data tables, Invoices, Receipts, Reports, Payroll |
| D146 | `EDIT(2).png` | Record Editing Drawers | Modal / Popover / Widget (499x528) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D147 | `Edit company.png` | Organization & Settings | Modal / Popover / Widget (579x389) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D148 | `Edit voucher-1.png` | Corporate Vouchers | Screen (579x1133) | Issue company vouchers, view voucher details, manage settlement and approval | Payroll, Wallet, Expenses |
| D149 | `Edit voucher.png` | Corporate Vouchers | Screen (579x1212) | Issue company vouchers, view voucher details, manage settlement and approval | Payroll, Wallet, Expenses |
| D150 | `Edit(1).png` | Record Editing Drawers | Modal / Popover / Widget (499x528) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D151 | `Edit-1(1).png` | Record Editing Drawers | Modal / Popover / Widget (499x528) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D152 | `Edit-2(1).png` | Record Editing Drawers | Screen (532x789) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D153 | `Edit-3(1).png` | Record Editing Drawers | Screen (500x752) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D154 | `Edit-4(1).png` | Record Editing Drawers | Modal / Popover / Widget (500x463) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D155 | `Ellipse 3009.png` | Design System & UI Components | Icon / Control / Badge (68x68) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D156 | `Ellipse 3019.png` | Design System & UI Components | Modal / Popover / Widget (535x535) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D157 | `Expenses inventory.png` | Platform Utilities & Views | Desktop Full Page (1438x970) | Supporting UI view or dialog component | General Navigation |
| D158 | `FILTER PAY.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x500) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D159 | `FILTER-1.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D160 | `FILTER-2.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D161 | `FILTER-3.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x406) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D162 | `FILTER-4.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x406) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D163 | `FILTER-5.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D164 | `FILTER-6.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D165 | `FILTER-7.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D166 | `FILTER-8.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D167 | `FILTER.png` | Table Filters & Search Popovers | Modal / Popover / Widget (445x561) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D168 | `Filter(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x73) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D169 | `Filter-1(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x119) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D170 | `Filter-2(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x119) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D171 | `Filter-3(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (142x87) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D172 | `Filter-4(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x87) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D173 | `Filter-5(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x119) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D174 | `Filter-6(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x118) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D175 | `Filter-7(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x81) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D176 | `Filter-8(1).png` | Table Filters & Search Popovers | Icon / Control / Badge (103x118) | Filter popover with date ranges, categories, statuses, and payment modes | Inventory, Sales, Payroll, Invoices, Transactions |
| D177 | `Frame 10289377.png` | Design System & UI Components | Modal / Popover / Widget (472x475) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D178 | `Frame 10289378.png` | Design System & UI Components | Modal / Popover / Widget (472x475) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D179 | `Frame 10289379.png` | Design System & UI Components | Modal / Popover / Widget (472x475) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D180 | `Frame 10289380.png` | Design System & UI Components | Modal / Popover / Widget (472x411) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D181 | `Frame 10289381.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D182 | `Frame 10289382.png` | Design System & UI Components | Modal / Popover / Widget (472x303) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D183 | `Frame 10289383.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D184 | `Frame 10289384.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D185 | `Frame 10289385.png` | Design System & UI Components | Modal / Popover / Widget (472x303) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D186 | `Frame 10289386.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D187 | `Frame 10289387.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D188 | `Frame 10289388.png` | Design System & UI Components | Modal / Popover / Widget (472x303) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D189 | `Frame 10289389.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D190 | `Frame 10289390.png` | Design System & UI Components | Modal / Popover / Widget (472x303) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D191 | `Frame 10289391.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D192 | `Frame 10289392.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D193 | `Frame 10289393.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D194 | `Frame 10289449.png` | Design System & UI Components | Screen (581x825) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D195 | `Frame 10289450.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D196 | `Frame 10289451.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D197 | `Frame 10289453.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D198 | `Frame 10289454.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D199 | `Frame 10289455.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D200 | `Frame 10289456.png` | Design System & UI Components | Modal / Popover / Widget (472x292) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D201 | `Frame 42.png` | Design System & UI Components | Modal / Popover / Widget (422x190) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D202 | `Frame 427319103.png` | Design System & UI Components | Icon / Control / Badge (188x90) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D203 | `Frame 427319104.png` | Design System & UI Components | Modal / Popover / Widget (608x290) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D204 | `Frame 427319105.png` | Design System & UI Components | Screen (881x676) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D205 | `Frame 427319106.png` | Design System & UI Components | Modal / Popover / Widget (608x290) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D206 | `Frame 427319107.png` | Design System & UI Components | Screen (867x712) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D207 | `Frame 427319108.png` | Design System & UI Components | Modal / Popover / Widget (608x290) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D208 | `Frame 427319109.png` | Design System & UI Components | Screen (878x690) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D209 | `Frame 427319110.png` | Design System & UI Components | Modal / Popover / Widget (608x290) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D210 | `Frame 49.png` | Design System & UI Components | Modal / Popover / Widget (422x190) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D211 | `Frame 50.png` | Design System & UI Components | Modal / Popover / Widget (422x190) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D212 | `Frame 512817.png` | Design System & UI Components | Desktop Full Page (1353x544) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D213 | `Frame 512836.png` | Design System & UI Components | Screen (906x64) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D214 | `Frame 512843.png` | Design System & UI Components | Desktop Full Page (1321x58) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D215 | `Frame 56.png` | Design System & UI Components | Screen (704x274) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D216 | `Frame 87.png` | Design System & UI Components | Screen (867x682) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D217 | `Frequency.png` | Platform Utilities & Views | Modal / Popover / Widget (566x380) | Supporting UI view or dialog component | General Navigation |
| D218 | `Good Morning.png` | Executive Dashboards | Icon / Control / Badge (187x28) | Aggregated financial metrics, active user stats, wallet balance, recent announcements and shortcuts | Wallet, Organizations, Individuals, Analytics |
| D219 | `Grant Permission-1.png` | Access Control & Roles | Modal / Popover / Widget (443x298) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D220 | `Grant Permission-2.png` | Access Control & Roles | Modal / Popover / Widget (443x298) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D221 | `Grant Permission-3.png` | Access Control & Roles | Modal / Popover / Widget (443x298) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D222 | `Grant Permission-4.png` | Access Control & Roles | Modal / Popover / Widget (443x298) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D223 | `Grant Permission-5.png` | Access Control & Roles | Modal / Popover / Widget (443x298) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D224 | `Grant Permission.png` | Access Control & Roles | Modal / Popover / Widget (443x298) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D225 | `Group 37996.png` | Design System & UI Components | Modal / Popover / Widget (405x78) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D226 | `Group 37999.png` | Design System & UI Components | Modal / Popover / Widget (405x78) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D227 | `Group 418.png` | Design System & UI Components | Modal / Popover / Widget (438x138) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D228 | `Group 482507.png` | Design System & UI Components | Icon / Control / Badge (80x42) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D229 | `Group 513033.png` | Design System & UI Components | Modal / Popover / Widget (506x310) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D230 | `Hello Tayo.png` | Executive Dashboards | Icon / Control / Badge (71x16) | Aggregated financial metrics, active user stats, wallet balance, recent announcements and shortcuts | Wallet, Organizations, Individuals, Analytics |
| D231 | `Individual Request.png` | Individual Banking & KYC | Desktop Full Page (1440x998) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D232 | `Individual Savings Plan.png` | Savings & Investments | Desktop Full Page (1440x998) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D233 | `Individual Wallet.png` | Wallet, Transfers & Funding | Desktop Full Page (1440x998) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D234 | `Individual verified-1.png` | Individual Banking & KYC | Desktop Full Page (1440x1080) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D235 | `Individual verified-2.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D236 | `Individual verified-3.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D237 | `Individual verified-4.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D238 | `Individual verified-5.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D239 | `Individual verified-6.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D240 | `Individual verified-7.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D241 | `Individual verified-8.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D242 | `Individual verified-9.png` | Individual Banking & KYC | Modal / Popover / Widget (443x298) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D243 | `Individual verified.png` | Individual Banking & KYC | Desktop Full Page (1440x1080) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D244 | `Individual-1.png` | Individual Banking & KYC | Desktop Full Page (1440x998) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D245 | `Individual.png` | Individual Banking & KYC | Icon / Control / Badge (76x14) | Retail user profile, KYC document submission, individual wallet and savings management | KYC, Wallet, Savings |
| D246 | `Information Technology.png` | Platform Utilities & Views | Screen (659x905) | Supporting UI view or dialog component | General Navigation |
| D247 | `Inventory.png` | ERP: Inventory & Catalog | Desktop Full Page (1440x1086) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D248 | `Invite code.png` | Staff & Workforce HR | Modal / Popover / Widget (659x559) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D249 | `Invite users.png` | Staff & Workforce HR | Desktop Full Page (1440x1024) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D250 | `Invited succ.png` | Staff & Workforce HR | Modal / Popover / Widget (472x292) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D251 | `Invoice generator.png` | Invoicing & Receivables | Desktop Full Page (1440x1164) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D252 | `Invoice settings(Account).png` | Invoicing & Receivables | Desktop Full Page (1440x1164) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D253 | `Invoice settings(Contact).png` | Invoicing & Receivables | Desktop Full Page (1440x1164) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D254 | `Invoice settings(tags).png` | Invoicing & Receivables | Desktop Full Page (1440x1164) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D255 | `Invoice settings.png` | Invoicing & Receivables | Desktop Full Page (1440x1164) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D256 | `Items-1.png` | ERP: Inventory & Catalog | Modal / Popover / Widget (286x521) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D257 | `Items.png` | ERP: Inventory & Catalog | Modal / Popover / Widget (286x521) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D258 | `Job Offer-1.png` | Recruitment & ATS | Screen (659x1284) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D259 | `Job Offer-2.png` | Recruitment & ATS | Screen (659x776) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D260 | `Job Offer.png` | Recruitment & ATS | Screen (659x776) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D261 | `Job Pub.png` | Recruitment & ATS | Modal / Popover / Widget (472x292) | Publish job vacancies, review applicant resumes, manage hiring pipeline stages | Public Recruitment, Staff |
| D262 | `Loan Request.png` | Lending & Credit | Modal / Popover / Widget (659x584) | Corporate loan plans, employee loan requests, repayment tracking and approval | Payroll, Wallet, Staff |
| D263 | `Loans View-1.png` | Lending & Credit | Screen (659x671) | Corporate loan plans, employee loan requests, repayment tracking and approval | Payroll, Wallet, Staff |
| D264 | `Loans View-2.png` | Lending & Credit | Modal / Popover / Widget (659x501) | Corporate loan plans, employee loan requests, repayment tracking and approval | Payroll, Wallet, Staff |
| D265 | `Loans View.png` | Lending & Credit | Screen (659x982) | Corporate loan plans, employee loan requests, repayment tracking and approval | Payroll, Wallet, Staff |
| D266 | `Manage Depts.png` | Organization Structure | Screen (659x671) | Create, update, and organize company departments | Staff, Roles, Salary Levels |
| D267 | `Manage Groups.png` | Thrift & Esusu Groups | Screen (659x786) | Rotational contribution groups (Ajo/Esusu), position picking, cycle progress, and payouts | Savings, Wallet, Staff |
| D268 | `Manage L.png` | Platform Utilities & Views | Modal / Popover / Widget (393x266) | Supporting UI view or dialog component | General Navigation |
| D269 | `MemberProfile.png` | Staff & Workforce HR | Desktop Full Page (1440x1080) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D270 | `Mnage Company.png` | Organization & Settings | Desktop Full Page (1440x1164) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D271 | `Notification.png` | Platform Utilities & Views | Screen (613x892) | Supporting UI view or dialog component | General Navigation |
| D272 | `Order Customer History.png` | ERP: Orders Management | Desktop Full Page (1438x813) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D273 | `Organisations Savings Plan.png` | Savings & Investments | Desktop Full Page (1440x998) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D274 | `Organisations Wallet.png` | Wallet, Transfers & Funding | Desktop Full Page (1440x998) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D275 | `Organization 2.png` | Organization & Settings | Desktop Full Page (1440x884) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D276 | `Organization 3.png` | Organization & Settings | Desktop Full Page (1440x884) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D277 | `Organization 4.png` | Organization & Settings | Desktop Full Page (1440x884) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D278 | `Organization 5.png` | Organization & Settings | Desktop Full Page (1440x884) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D279 | `Organization-1.png` | Organization & Settings | Desktop Full Page (1440x998) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D280 | `Organization-2.png` | Organization & Settings | Desktop Full Page (2997x506) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D281 | `Organization.png` | Organization & Settings | Icon / Control / Badge (102x18) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D282 | `Organizqation-1.png` | Organization & Settings | Screen (659x671) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D283 | `Organizqation.png` | Organization & Settings | Screen (659x671) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D284 | `PURCHASE-1.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x970) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D285 | `PURCHASE-2.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x970) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D286 | `PURCHASE-3.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x970) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D287 | `PURCHASE.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x1053) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D288 | `Pay all.png` | Corporate Payroll | Modal / Popover / Widget (472x292) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D289 | `Pay by level-1.png` | Corporate Payroll | Screen (659x786) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D290 | `Pay by level.png` | Corporate Payroll | Screen (659x847) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D291 | `Pay p.png` | Corporate Payroll | Modal / Popover / Widget (472x475) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D292 | `Payment Option-1.png` | Platform Utilities & Views | Icon / Control / Badge (166x118) | Supporting UI view or dialog component | General Navigation |
| D293 | `Payment Option.png` | Platform Utilities & Views | Icon / Control / Badge (166x118) | Supporting UI view or dialog component | General Navigation |
| D294 | `Payment mode.png` | Platform Utilities & Views | Icon / Control / Badge (166x118) | Supporting UI view or dialog component | General Navigation |
| D295 | `Payroll(Analytics)-1.png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D296 | `Payroll(Analytics).png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D297 | `Payroll(History)-1.png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D298 | `Payroll(History).png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D299 | `Payroll(Schedule-1.png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D300 | `Payroll(Schedule-2.png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D301 | `Payroll(Schedule-3.png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D302 | `Payroll(Schedule.png` | Corporate Payroll | Desktop Full Page (1440x1164) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D303 | `Payslip.png` | Corporate Payroll | Screen (659x671) | Schedule payroll runs, preview salary calculations, review batch history, view employee payslips | Staff, Salary Levels, Departments, Vouchers |
| D304 | `Personal.png` | Platform Utilities & Views | Modal / Popover / Widget (472x292) | Supporting UI view or dialog component | General Navigation |
| D305 | `Policy Type.png` | Platform Utilities & Views | Icon / Control / Badge (150x128) | Supporting UI view or dialog component | General Navigation |
| D306 | `Profile oganiza-1.png` | Organization & Settings | Desktop Full Page (1440x1218) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D307 | `Profile oganiza-2.png` | Organization & Settings | Desktop Full Page (1440x1218) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D308 | `Profile oganiza-3.png` | Organization & Settings | Desktop Full Page (1440x1218) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D309 | `Profile oganiza-4.png` | Organization & Settings | Desktop Full Page (1440x1218) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D310 | `Profile oganiza.png` | Organization & Settings | Desktop Full Page (1440x1218) | Company profile, KYB registration, business settings, organization verification status | KYB, Compliance, Settings |
| D311 | `Profile-1.png` | Platform Utilities & Views | Screen (528x997) | Supporting UI view or dialog component | General Navigation |
| D312 | `Profile.png` | Platform Utilities & Views | Screen (528x1040) | Supporting UI view or dialog component | General Navigation |
| D313 | `Purchase inventory profilt and loss for the Day.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x1051) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D314 | `Purchase inventory profilt and loss for the Month.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x1051) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D315 | `Purchase inventory profilt and loss for the Week.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x1051) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D316 | `Purchase inventory profilt and loss for the Year.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x1051) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D317 | `Real Inventory.png` | ERP: Inventory & Catalog | Desktop Full Page (1440x1086) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D318 | `Rectangle 17988.png` | Design System & UI Components | Desktop Full Page (16532x1266) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D319 | `Rectangle 17989.png` | Design System & UI Components | Desktop Full Page (16532x1266) | Figma design system atom, card primitive, chart graphic, or artboard export | Design Tokens, Common UI Library |
| D320 | `Roles.png` | Access Control & Roles | Screen (659x671) | RBAC configuration, assign permissions to administrative and organizational roles | Staff, Admin Manage, Security |
| D321 | `Saving Plans-1.png` | Savings & Investments | Screen (659x1040) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D322 | `Saving Plans-2.png` | Savings & Investments | Screen (659x962) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D323 | `Saving Plans-3.png` | Savings & Investments | Screen (659x1159) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D324 | `Saving Plans.png` | Savings & Investments | Screen (659x937) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D325 | `Send Invoice.png` | Invoicing & Receivables | Icon / Control / Badge (228x100) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D326 | `Service BoughtEdit.png` | ERP: Services Catalog | Modal / Popover / Widget (498x315) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D327 | `Service Boughtadd.png` | ERP: Services Catalog | Modal / Popover / Widget (498x315) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D328 | `Service rendered Edit.png` | ERP: Services Catalog | Modal / Popover / Widget (498x315) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D329 | `Service rendered add.png` | ERP: Services Catalog | Modal / Popover / Widget (498x315) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D330 | `ServicesCategories.png` | ERP: Services Catalog | Desktop Full Page (1443x825) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D331 | `Staff.png` | Staff & Workforce HR | Desktop Full Page (1440x998) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D332 | `Subject deleted-17.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x267) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D333 | `Subject deleted-18.png` | Confirmation & Feedback Modals | Modal / Popover / Widget (358x267) | Modal confirming deletion of records (items, departments, roles, members) or post-deletion feedback | Departments, Roles, Items, Members, Invoices |
| D334 | `Successful-17.png` | Feedback & Success Modals | Icon / Control / Badge (240x208) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D335 | `Successful-18.png` | Feedback & Success Modals | Icon / Control / Badge (240x208) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D336 | `TLF.png` | Savings & Investments | Desktop Full Page (1440x1164) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D337 | `TSM.png` | Savings & Investments | Desktop Full Page (1440x1164) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D338 | `TSP.png` | Savings & Investments | Desktop Full Page (1440x1164) | Target savings, locked deposits, interest accrual monitoring for individuals and organizations | Wallet, Thrift, Admin Policies |
| D339 | `Tag.png` | Platform Utilities & Views | Modal / Popover / Widget (500x300) | Supporting UI view or dialog component | General Navigation |
| D340 | `Team Profile-1.png` | Staff & Workforce HR | Desktop Full Page (1440x1080) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D341 | `Team Profile.png` | Staff & Workforce HR | Desktop Full Page (1440x1080) | Workforce directory, employee profiles, invite staff members, manage suspensions and status | Departments, Roles, Salary Levels, Payroll |
| D342 | `Transfer Fund-1.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x313) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D343 | `Transfer Fund.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x313) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D344 | `Transfer Payment MODE-1.png` | Wallet, Transfers & Funding | Desktop Full Page (1438x1051) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D345 | `Transfer Payment MODE-2.png` | Wallet, Transfers & Funding | Desktop Full Page (1438x1051) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D346 | `Transfer Payment MODE.png` | Wallet, Transfers & Funding | Desktop Full Page (1438x1051) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D347 | `Transfer to bank-1.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D348 | `Transfer to bank-2.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D349 | `Transfer to bank-3.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D350 | `Transfer to bank-4.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D351 | `Transfer to bank-5.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D352 | `Transfer to bank.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D353 | `View Report-1.png` | Platform Utilities & Views | Icon / Control / Badge (166x148) | Supporting UI view or dialog component | General Navigation |
| D354 | `View Report.png` | Platform Utilities & Views | Icon / Control / Badge (166x118) | Supporting UI view or dialog component | General Navigation |
| D355 | `Voucher.png` | Corporate Vouchers | Desktop Full Page (1440x1164) | Issue company vouchers, view voucher details, manage settlement and approval | Payroll, Wallet, Expenses |
| D356 | `Wallet Org.-1.png` | Wallet, Transfers & Funding | Desktop Full Page (1440x1164) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D357 | `Wallet Org..png` | Wallet, Transfers & Funding | Desktop Full Page (1440x1164) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D358 | `Withdraw Via Card-1.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D359 | `Withdraw Via Card-2.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D360 | `Withdraw Via Card.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D361 | `Withdraw Via Merchant-1.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D362 | `Withdraw Via Merchant-2.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D363 | `Withdraw Via Merchant-3.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D364 | `Withdraw Via Merchant-4.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D365 | `Withdraw Via Merchant-5.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D366 | `Withdraw Via Merchant-6.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D367 | `Withdraw Via Merchant-7.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D368 | `Withdraw Via Merchant-8.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x475) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D369 | `Withdraw Via Merchant.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x407) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D370 | `Withdraw Via.png` | Wallet, Transfers & Funding | Modal / Popover / Widget (472x313) | P2P wallet transfer, NIP bank transfers, wallet funding via card/DVA, merchant withdrawal | Cards, Bank Accounts, Virtual Accounts |
| D371 | `add New Admin-1.png` | Platform Utilities & Views | Modal / Popover / Widget (472x432) | Supporting UI view or dialog component | General Navigation |
| D372 | `add New Admin.png` | Platform Utilities & Views | Modal / Popover / Widget (472x543) | Supporting UI view or dialog component | General Navigation |
| D373 | `add items 2.png` | ERP: Inventory & Catalog | Screen (500x802) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D374 | `add items.png` | ERP: Inventory & Catalog | Screen (500x802) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D375 | `customer History.png` | CRM: Customer Management | Desktop Full Page (1435x821) | Customer accounts, purchase history, outstanding receivables, and contact details | Sales, Invoices, Receipts, Orders |
| D376 | `customer details.png` | CRM: Customer Management | Desktop Full Page (1435x777) | Customer accounts, purchase history, outstanding receivables, and contact details | Sales, Invoices, Receipts, Orders |
| D377 | `done-1.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D378 | `done-10.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D379 | `done-11.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D380 | `done-12.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D381 | `done-13.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D382 | `done-14.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D383 | `done-15.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D384 | `done-16.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D385 | `done-17.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D386 | `done-18.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D387 | `done-19.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D388 | `done-2.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D389 | `done-20.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D390 | `done-21.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D391 | `done-22.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D392 | `done-23.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D393 | `done-24.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D394 | `done-25.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D395 | `done-26.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D396 | `done-27.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D397 | `done-28.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D398 | `done-29.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D399 | `done-3.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D400 | `done-30.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D401 | `done-31.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D402 | `done-4.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D403 | `done-5.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D404 | `done-6.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D405 | `done-7.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D406 | `done-8.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D407 | `done-9.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D408 | `done.png` | Feedback & Success Modals | Modal / Popover / Widget (438x324) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D409 | `edit-1.png` | Record Editing Drawers | Screen (500x802) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D410 | `edit-2.png` | Record Editing Drawers | Screen (500x802) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D411 | `edit-3.png` | Record Editing Drawers | Mobile Viewport (498x735) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D412 | `edit-4.png` | Record Editing Drawers | Mobile Viewport (499x750) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D413 | `edit-5.png` | Record Editing Drawers | Screen (500x752) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D414 | `edit-6.png` | Record Editing Drawers | Screen (500x752) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D415 | `edit.png` | Record Editing Drawers | Screen (500x802) | Modal/drawer form for editing existing record attributes | Inventory, Suppliers, Customers, Services |
| D416 | `finance tray.png` | Platform Utilities & Views | Icon / Control / Badge (155x230) | Supporting UI view or dialog component | General Navigation |
| D417 | `iPhone 14 Pro - 101.png` | Mobile Responsive Views | Modal / Popover / Widget (393x513) | Mobile responsive card selection, payment drawers, mobile navigation patterns | Cards, Wallet, Checkout |
| D418 | `iPhone 14 Pro - 102.png` | Mobile Responsive Views | Modal / Popover / Widget (393x266) | Mobile responsive card selection, payment drawers, mobile navigation patterns | Cards, Wallet, Checkout |
| D419 | `iPhone 14 Pro - 91.png` | Mobile Responsive Views | Modal / Popover / Widget (393x400) | Mobile responsive card selection, payment drawers, mobile navigation patterns | Cards, Wallet, Checkout |
| D420 | `iPhone 14 Pro - 97.png` | Mobile Responsive Views | Modal / Popover / Widget (393x266) | Mobile responsive card selection, payment drawers, mobile navigation patterns | Cards, Wallet, Checkout |
| D421 | `iPhone 14 Pro - 98.png` | Mobile Responsive Views | Modal / Popover / Widget (393x352) | Mobile responsive card selection, payment drawers, mobile navigation patterns | Cards, Wallet, Checkout |
| D422 | `iPhone 14 Pro - 99.png` | Mobile Responsive Views | Modal / Popover / Widget (393x352) | Mobile responsive card selection, payment drawers, mobile navigation patterns | Cards, Wallet, Checkout |
| D423 | `items Categories.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x825) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D424 | `items details-1.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x780) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D425 | `items details.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x810) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D426 | `items inventory 2.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x812) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D427 | `items inventory 3.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x1051) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D428 | `items inventory 4.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x811) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D429 | `items inventory 5.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x812) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D430 | `items inventory 6.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x812) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D431 | `items inventory-1.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x783) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D432 | `items inventory-2.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x812) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D433 | `items inventory.png` | ERP: Inventory & Catalog | Desktop Full Page (1443x812) | Catalog management, stock levels, stock movement tracking, valuation and item categories | Purchases, Sales, Suppliers, Valuation Policy |
| D434 | `manage customer-1.png` | CRM: Customer Management | Desktop Full Page (1438x1052) | Customer accounts, purchase history, outstanding receivables, and contact details | Sales, Invoices, Receipts, Orders |
| D435 | `manage customer.png` | CRM: Customer Management | Desktop Full Page (1438x777) | Customer accounts, purchase history, outstanding receivables, and contact details | Sales, Invoices, Receipts, Orders |
| D436 | `menu-1.png` | Platform Utilities & Views | Icon / Control / Badge (170x119) | Supporting UI view or dialog component | General Navigation |
| D437 | `menu-2.png` | Platform Utilities & Views | Icon / Control / Badge (170x119) | Supporting UI view or dialog component | General Navigation |
| D438 | `menu-3.png` | Platform Utilities & Views | Icon / Control / Badge (170x119) | Supporting UI view or dialog component | General Navigation |
| D439 | `menu.png` | Platform Utilities & Views | Icon / Control / Badge (170x119) | Supporting UI view or dialog component | General Navigation |
| D440 | `order details-1.png` | ERP: Orders Management | Desktop Full Page (1438x805) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D441 | `order details.png` | ERP: Orders Management | Desktop Full Page (1438x806) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D442 | `order inventory-1.png` | ERP: Orders Management | Desktop Full Page (1438x806) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D443 | `order inventory.png` | ERP: Orders Management | Desktop Full Page (1438x784) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D444 | `oredr inventory-1.png` | ERP: Orders Management | Desktop Full Page (1466x845) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D445 | `oredr inventory-2.png` | ERP: Orders Management | Desktop Full Page (1461x828) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D446 | `oredr inventory.png` | ERP: Orders Management | Desktop Full Page (1461x833) | Manage customer sales orders, line items, fulfillment status, and order customer history | Customers, Sales, Inventory, Invoices |
| D447 | `purchase details History.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x784) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D448 | `purchase details.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x784) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D449 | `purchase inventory.png` | ERP: Purchases & Procurement | Desktop Full Page (1438x970) | Create, manage, and view purchase orders, procurement history, and P&L metrics | Suppliers, Inventory Items, Expenses, Receipts |
| D450 | `sales details-1.png` | ERP: Sales & Orders | Desktop Full Page (1438x822) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D451 | `sales details.png` | ERP: Sales & Orders | Desktop Full Page (1438x776) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D452 | `sales inventory profilt and loss for the Day.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D453 | `sales inventory profilt and loss for the Month.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D454 | `sales inventory profilt and loss for the Year.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D455 | `sales inventory profilt and loss for the week.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D456 | `sales inventory-1.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D457 | `sales inventory-2.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D458 | `sales inventory-3.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D459 | `sales inventory.png` | ERP: Sales & Orders | Desktop Full Page (1438x1051) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D460 | `sales.png` | ERP: Sales & Orders | Icon / Control / Badge (116x80) | Record customer sales, view sales orders, track daily/monthly P&L metrics | Customers, Inventory Items, Invoices, Receipts |
| D461 | `service details-1.png` | ERP: Services Catalog | Desktop Full Page (1435x807) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D462 | `service details.png` | ERP: Services Catalog | Desktop Full Page (1435x807) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D463 | `service inventory-1.png` | ERP: Services Catalog | Desktop Full Page (1439x776) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D464 | `service inventory-2.png` | ERP: Services Catalog | Desktop Full Page (1438x807) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D465 | `service inventory-3.png` | ERP: Services Catalog | Desktop Full Page (1438x806) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D466 | `service inventory.png` | ERP: Services Catalog | Desktop Full Page (1438x807) | Manage billable services, hourly/fixed service definitions, and service categories | Invoicing, Customers, Sales |
| D467 | `successfuly added sa.png` | Feedback & Success Modals | Modal / Popover / Widget (594x456) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D468 | `successfuly added se.png` | Feedback & Success Modals | Modal / Popover / Widget (588x488) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D469 | `successfuly added su.png` | Feedback & Success Modals | Modal / Popover / Widget (550x497) | Visual confirmation modal displayed after completing an action (e.g. created, updated, transferred) | Transfers, Create Item, Add Role, Add Staff, Submit KYC |
| D470 | `supplier's inventory-1.png` | ERP: Supplier Management | Desktop Full Page (1438x805) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D471 | `supplier's inventory.png` | ERP: Supplier Management | Desktop Full Page (1438x808) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D472 | `suppliers details-1.png` | ERP: Supplier Management | Desktop Full Page (1438x1051) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D473 | `suppliers details.png` | ERP: Supplier Management | Desktop Full Page (1438x801) | Vendor directory, vendor contact information, supply histories, and purchase orders | Purchases, Expenses, Inventory |
| D474 | `view invoice Order-1.png` | Invoicing & Receivables | Desktop Full Page (1439x1330) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D475 | `view invoice Order.png` | Invoicing & Receivables | Desktop Full Page (1439x1330) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D476 | `view invoice p-1.png` | Invoicing & Receivables | Desktop Full Page (1438x1231) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D477 | `view invoice p.png` | Invoicing & Receivables | Desktop Full Page (1438x1231) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D478 | `view invoice sa.png` | Invoicing & Receivables | Desktop Full Page (1439x1330) | Create professional invoices, configure invoice templates/tags, track invoice payment status | Customers, Sales, Receipts, Wallet |
| D479 | `view voucher.png` | Corporate Vouchers | Screen (947x982) | Issue company vouchers, view voucher details, manage settlement and approval | Payroll, Wallet, Expenses |

---

## 5. Visual Asset Retrieval Guide for Implementation

When implementing frontend features, developers should follow this targeted retrieval procedure rather than browsing all images:

```text
1. Identify Target Feature (e.g. Corporate Payroll Processing)
   ↓
2. Query Design Index for Family (Family 6: Corporate Payroll)
   ↓
3. Identify Specific Visual References:
   - Primary View: Payroll(Schedule.png (D302)
   - History & Analytics: Payroll(History).png (D298), Payroll(Analytics).png (D296)
   - Action Modals: Pay by level.png (D290), Create voucher.png (D082)
   - Feedback & Alerts: done-1.png (D377)
   - Table Controls: FILTER PAY.png (D158), Download-1.png (D111)
   ↓
4. Inspect Only Selected Reference Files
   ↓
5. Implement Feature in React + Tailwind CSS
```
