# CebizPay Web Frontend — Phase 1

## Backend Discovery, Design-Library Indexing & Frontend Architecture Analysis

You are preparing to build the web frontend for **CebizPay**, a production fintech application.

The frontend **does not exist yet**. This is a greenfield frontend implementation.

Your responsibility in this phase is to perform a comprehensive analysis of:

1. The existing backend codebase
2. The complete available API surface
3. Authentication and authorization
4. The supplied design library(frontend/design-library)
5. The application's likely web-facing features and pages
6. The relationship between backend capabilities and frontend requirements

Then produce a practical frontend implementation blueprint.

### CRITICAL: DO NOT IMPLEMENT THE FRONTEND IN THIS PHASE.

Do not create React components, pages, API services, layouts, routes, or other frontend implementation code yet.

The only implementation-related artifact you should create is the analysis/specification documentation requested at the end of this prompt.

---

# 1. Technology Constraints

The frontend will use:

* React
* JavaScript
* Tailwind CSS v4

### Strict requirements

* Use **JavaScript, NOT TypeScript**.
* Do not introduce TypeScript files.
* Do not replace React with another frontend framework.
* Use Tailwind CSS v4.
* Use React Router if routing is required.
* Avoid unnecessary dependencies.
* Do not introduce a state-management library unless the analysis demonstrates that it is actually necessary.
* Prefer simple, maintainable architecture over unnecessary abstraction.

The final frontend should be a professional, production-oriented application, but it should remain reasonably simple.

---

# 2. Repository / Project Context

The current project contains the existing CebizPay backend.

The frontend has not yet been implemented.

You must therefore treat the frontend as a new application that needs to be designed and built around the existing backend.

Do not assume that an existing frontend architecture exists.

Do not create frontend architecture before understanding the backend and design references.

---

# 3. DESIGN LIBRARY — IMPORTANT

A directory containing the application's design references has been provided.

Treat this directory as the:

# AUTHORITATIVE VISUAL DESIGN LIBRARY

The directory contains **500+ screen images/design references**.

The images represent many screens from the product, but they may not represent every screen that will ultimately be required.

The designs should therefore be treated as:

* Visual references
* Product design references
* Design-system references
* Navigation references
* Component references
* Interaction-pattern references

They are NOT necessarily a complete page inventory.

---

# 4. DO NOT LOAD ALL 500+ IMAGES INTO CONTEXT

This is a critical requirement.

Do NOT attempt to inspect or load all 500+ images into your active context simultaneously.

Instead, treat the design directory as a **virtual visual asset library**.

Your workflow should be:

```text
Design Library
      ↓
Inspect directory
      ↓
Inventory filenames / structure
      ↓
Create searchable design index
      ↓
Identify relevant references for each feature/page
      ↓
Inspect only relevant images
      ↓
Use those references during implementation
```

The purpose is to make the entire design library available for retrieval without unnecessarily consuming context.

---

# 5. DESIGN LIBRARY INVENTORY

Before implementing anything, inspect the design directory.

Determine:

* Directory structure
* File names
* File types
* Naming conventions
* Existing grouping/categories
* Obvious feature areas
* Desktop/mobile variants
* Related screens
* Duplicate/alternate versions where identifiable

Use the filenames and directory structure as the initial retrieval mechanism.

Do not assume the filename is always perfectly descriptive.

Where useful, inspect the actual image to verify what the filename represents.

---

# 6. CREATE A DESIGN LIBRARY INDEX

Create:

`docs/design/DESIGN-LIBRARY-INDEX.md`

This should serve as a searchable catalogue of the visual library.

For every reasonably identifiable screen, record information such as:

| ID | File | Area | Screen | Purpose | Related Screens |
| -- | ---- | ---- | ------ | ------- | --------------- |

For example:

```text
D001
File: customer-dashboard-overview.png
Area: Customer Dashboard
Screen: Dashboard Overview
Purpose: Main customer dashboard
Related: wallet, transactions, notifications
```

Do not fabricate descriptions when the image cannot be confidently identified.

Use the actual file names.

Where the filename is ambiguous, inspect the image.

---

# 7. CREATE DESIGN FAMILIES / GROUPS

Do not treat the 500+ images as 500 unrelated screens.

Identify related screen families and workflows.

For example:

```text
Wallet
├── Wallet Overview
├── Wallet Transactions
├── Transaction Details
├── Deposit
├── Withdrawal
└── Wallet Settings
```

Or:

```text
Transfers
├── Transfer Overview
├── Bank Transfer
├── Transfer Confirmation
├── Transfer Success
├── Transfer Failed
└── Transfer Details
```

Create logical groups based on what you can actually observe.

The goal is to make it possible to answer:

> "What designs are relevant when implementing this feature?"

without searching the entire library every time.

---

# 8. CREATE A DESIGN-SYSTEM REFERENCE

Create:

`docs/design/DESIGN-SYSTEM-REFERENCE.md`

Analyze the visual language across the design library.

Do not derive the design system from only one or two screens.

Use multiple representative screens to identify recurring patterns.

Analyze:

## Typography

Identify:

* Font family
* Heading hierarchy
* Body text
* Labels
* Captions
* Font sizes
* Font weights
* Line heights
* Letter spacing where relevant

## Colors

Identify recurring:

* Primary colors
* Secondary colors
* Backgrounds
* Surfaces
* Cards
* Text
* Muted text
* Borders
* Success
* Warning
* Error
* Information
* Interactive states

## Spacing

Identify recurring:

* Page padding
* Section spacing
* Card padding
* Component spacing
* Grid gaps
* Form spacing
* Navigation spacing

## Borders / Radius / Shadows

Identify:

* Border usage
* Border thickness
* Border radius
* Card radius
* Input radius
* Modal radius
* Shadow patterns

## Components

Identify recurring patterns such as:

* Buttons
* Inputs
* Selects
* Dropdowns
* Cards
* Tables
* Tabs
* Badges
* Modals
* Drawers
* Alerts
* Toasts
* Pagination
* Breadcrumbs
* Search
* Filters
* Avatars
* Transaction rows
* Balance cards
* Statistics cards
* Empty states
* Loading states
* Error states

For each recurring pattern, determine whether it should become a reusable frontend component.

---

# 9. ICONOGRAPHY

Pay special attention to icon usage in the designs.

The final application must NOT have the visual appearance of an AI-generated dashboard.

Do not add icons simply because an element could have an icon.

Analyze:

* Which elements actually use icons
* Which elements do not
* Icon style
* Stroke vs filled style
* Stroke weight
* Icon size
* Icon placement
* Icon spacing
* Navigation icon patterns
* Action icon patterns

Recommend the most appropriate established React icon library based on the actual visual style.

Do not choose a library simply because it is popular.

Do not mix visually inconsistent icon families without a clear reason.

Do not introduce:

* Excessive icons
* Decorative icons with no purpose
* Generic AI-looking icons
* Random illustrations
* Unnecessary visual decoration

The final UI should feel deliberate, restrained, professional, and consistent with the supplied designs.

---

# 10. RESPONSIVE DESIGN ANALYSIS

Analyze responsive behavior from the available designs.

Determine patterns for:

* Desktop
* Laptop
* Tablet
* Mobile

Pay particular attention to:

* Sidebar behavior
* Mobile navigation
* Topbar
* Tables
* Cards
* Forms
* Grids
* Modals
* Drawers
* Page padding
* Typography
* Content hierarchy

If desktop and mobile versions of a screen exist, compare them directly.

Do not assume that responsive design simply means shrinking the desktop layout.

Document the responsive rules that can reasonably be inferred from the designs.

---

# 11. BACKEND ANALYSIS

Now thoroughly inspect the backend.

The backend is the **source of truth for application functionality and data**.

Analyze all relevant:

* Controllers
* Routes
* Endpoints
* HTTP methods
* DTOs
* Request models
* Response models
* Validation
* Authentication
* Authorization
* Roles
* Permissions
* JWT/token handling
* Error responses
* HTTP status codes
* Pagination
* Filtering
* Sorting
* Search
* File handling
* Wallet operations
* Payment operations
* Transfer operations
* Transaction operations
* User operations
* Notifications
* Administrative operations
* Other user-facing capabilities

Do not rely solely on controller names.

Trace important flows into the application/domain layers when necessary to understand what the endpoint actually does.

---

# 12. API INVENTORY

Create:

`docs/api/FRONTEND-API-INVENTORY.md`

Document the relevant API surface.

For each endpoint where possible, identify:

* Feature
* HTTP method
* Route
* Authentication requirement
* Required role/permission
* Request parameters
* Request body
* Response structure
* Validation
* Possible errors
* Pagination
* Filtering
* Sorting
* Search
* Important business behavior

Use the actual backend implementation as the source.

Do not invent missing details.

If something is unclear, explicitly mark it as unclear.

---

# 13. CLASSIFY BACKEND ENDPOINTS

Do not assume every backend endpoint needs to be consumed by the web application.

Classify endpoints where possible as:

* Public web
* Customer web
* Admin web
* Mobile-specific
* Internal/system
* Provider/webhook
* Background processing
* No frontend consumption required
* Unclear

The objective is not:

> "Put every API endpoint somewhere in the frontend."

The objective is:

> "Ensure every relevant web-facing backend capability has an appropriate frontend representation."

---

# 14. AUTHENTICATION & AUTHORIZATION ANALYSIS

Understand the backend's actual authentication system.

Document:

* Login
* Registration
* Logout
* Token issuance
* Token storage expectations
* Token expiration
* Refresh mechanism, if any
* Protected endpoints
* Roles
* Permissions
* Authorization rules
* Unauthorized responses
* Authentication failures
* Password/credential-related flows
* Any OTP or verification flows relevant to the web application

Do not invent authentication behavior.

Do not assume that simply decoding a JWT makes the frontend authorization model correct.

The backend remains authoritative for security.

---

# 15. API → UI MAPPING

Create:

`docs/api/API-TO-UI-MAPPING.md`

Map relevant backend capabilities to frontend experiences.

For example:

```text
Feature
    ↓
Endpoint
    ↓
Page
    ↓
UI operation
    ↓
Required state
```

Identify:

* Pages that consume GET endpoints
* Forms that consume POST/PUT/PATCH endpoints
* Delete operations
* Detail views
* Tables
* Filters
* Pagination
* Search
* Dashboard statistics
* Transaction flows
* Multi-step workflows
* Confirmation flows

Also identify backend capabilities that currently have no obvious web UI.

Do not silently ignore them.

---

# 16. PAGE INVENTORY

Using both:

1. Backend capabilities
2. Design-library analysis

derive a likely complete frontend page inventory.

Organize it logically, for example:

```text
Public Website
Authentication
Customer Application
Administrative Application
Account / Settings
Other Web Features
```

Do NOT restrict the page inventory to the screens that were explicitly supplied as designs.

The supplied design library is a visual/product reference.

If a backend feature requires a page that does not have an exact design reference, identify the closest related design patterns and document how the new page should inherit the existing design language.

---

# 17. VISUAL REFERENCE RETRIEVAL STRATEGY

Define how relevant designs should be retrieved during implementation.

For every major feature/page, identify the most relevant design references.

For example:

```text
Customer Wallet
├── wallet-overview.png
├── wallet-transactions.png
├── transaction-details.png
├── shared-table-reference.png
├── shared-filter-reference.png
└── shared-empty-state.png
```

Do not require an exact filename match.

When implementing a page, relevant references may include:

* The exact page
* Related pages
* Parent feature
* Shared layouts
* Shared components
* Mobile variants
* Similar forms
* Similar tables
* Similar empty/error states

This should allow the implementation agent to retrieve only the visual references needed for the current task.

---

# 18. NO MOCK DATA

This is a strict architectural requirement.

The frontend must NOT rely on:

* Mock data
* Fake API responses
* Fake users
* Fake transactions
* Fake balances
* Fake statistics
* Hardcoded business data
* Simulated backend responses
* Placeholder transaction records
* Fake dashboard metrics

All real application/business data must originate from the backend.

If a design contains information that the backend does not currently expose:

DO NOT fabricate it.

Instead:

1. Identify the missing capability.
2. Document it.
3. Determine whether the UI can gracefully omit or handle it.
4. Flag it for review.

Static UI configuration such as navigation labels, UI constants, feature labels, and visual configuration is acceptable.

Business/application data is not.

---

# 19. ERROR-HANDLING REQUIREMENTS

Analyze how the backend communicates errors and design a centralized frontend strategy.

Account for:

* Network errors
* Timeouts
* 400
* 401
* 403
* 404
* 409
* 422 where applicable
* 429
* 500+
* Validation errors
* Business-rule errors
* Unexpected responses

Determine reusable UI patterns for:

* Loading
* Empty
* Error
* Retry
* Success
* Form errors
* API errors
* Confirmation
* Toast/notification feedback

Do not allow each page to invent its own error-handling behavior.

---

# 20. FRONTEND ARCHITECTURE

Propose a clean React architecture based on everything you have discovered.

Determine:

* Folder structure
* Routing
* Layouts
* Pages
* Features
* Components
* API/service layer
* Authentication state
* Server/API state
* Form state
* Hooks
* Utilities
* Constants
* Error handling
* Loading states
* Empty states
* Responsive behavior

Prefer clear boundaries and understandable code.

Do not over-engineer the application.

---

# 21. WEBSITE + APPLICATION + ADMIN SEPARATION

The web repository may contain multiple experiences.

Determine whether the application needs:

* Public marketing website
* Authentication
* Customer dashboard/application
* Admin dashboard
* Other authenticated areas

Where appropriate, establish separate layouts.

For example:

```text
MarketingLayout
CustomerDashboardLayout
AdminDashboardLayout
AuthLayout
```

Do not force one navigation system onto fundamentally different application experiences.

---

# 22. SECURITY CONSIDERATIONS

Identify frontend security considerations arising from the backend architecture.

Pay attention to:

* Authentication
* Token handling
* Authorization
* Sensitive data
* Financial information
* User information
* API exposure
* XSS risks
* CSRF considerations where applicable
* Sensitive information in local storage
* Error-message exposure
* Client-side trust assumptions

Do not treat frontend authorization as a replacement for backend authorization.

---

# 23. IMPLEMENTATION ORDER

Recommend a practical implementation order.

For example:

```text
1. Project foundation
2. Tailwind/design tokens
3. Global components
4. API client
5. Authentication
6. Routing/layouts
7. Core dashboard
8. Core financial flows
9. Remaining features
10. Admin functionality
11. Responsive refinement
12. Error/loading/empty states
13. Integration verification
```

Adjust this based on the actual backend and design-library findings.

---

# 24. DOCUMENT IMPORTANT GAPS

Explicitly identify:

### Backend gaps

Where the frontend appears to require something that the backend does not currently provide.

### Design gaps

Where a required page has no direct design reference.

### API ambiguities

Where the API behavior or response structure is unclear.

### Product ambiguities

Where the backend and designs appear to disagree.

Do not silently make assumptions.

---

# 25. REQUIRED DELIVERABLES

At the end of this analysis phase, create the following documentation:

```text
docs/
├── FRONTEND-IMPLEMENTATION-PLAN.md
│
├── api/
│   ├── FRONTEND-API-INVENTORY.md
│   └── API-TO-UI-MAPPING.md
│
└── design/
    ├── DESIGN-LIBRARY-INDEX.md
    └── DESIGN-SYSTEM-REFERENCE.md
```

The documents should be concise enough to remain usable, but detailed enough to guide implementation.

They should contain concrete findings from the actual backend and design library.

Avoid generic frontend advice.

---

# 26. FINAL QUALITY CHECK

Before finishing Phase 1, verify that you have:

* Inspected the backend thoroughly.
* Inventoried the relevant API surface.
* Analyzed authentication.
* Analyzed authorization.
* Classified backend endpoints.
* Created an API-to-UI mapping.
* Inspected the design-library directory.
* Created a searchable design inventory.
* Identified design families.
* Analyzed the broader design system.
* Analyzed typography.
* Analyzed colors.
* Analyzed spacing.
* Analyzed reusable components.
* Analyzed navigation.
* Analyzed responsive behavior.
* Analyzed iconography.
* Identified appropriate icon-library direction.
* Identified likely frontend pages.
* Identified missing design references.
* Identified backend/design inconsistencies.
* Designed a centralized error-handling strategy.
* Proposed a clean React architecture.
* Proposed a practical implementation order.

---

# 27. ABSOLUTE RESTRICTIONS FOR THIS PHASE

DO NOT:

* Implement the frontend.
* Create mock data.
* Create fake API responses.
* Invent endpoints.
* Invent backend functionality.
* Invent business rules.
* Use TypeScript.
* Modify backend behavior.
* Redesign the product independently of the supplied design language.
* Load all 500+ images into context unnecessarily.
* Treat filenames as unquestionable truth.
* Add unnecessary icons.
* Recommend excessive dependencies.
* Proceed directly to implementation.

The purpose of this phase is to build a **reliable understanding of the backend and complete design library before implementation begins**.

When the analysis and documentation are complete, STOP.

Wait for explicit approval/instructions before beginning frontend implementation.
