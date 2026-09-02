# CebizPay Design System Reference

## 1. Visual Philosophy & Core Principles

CebizPay is an enterprise-grade fintech and workforce operations platform. The visual design language derived from the authoritative design library (`frontend/design-library/`) embodies:

1. **High Trust & Clarity**: Crisp typography, high contrast, clean white surfaces, and deliberate data density.
2. **Pill-Centric Modern Navigation**: Pill-shaped action buttons (`rounded-full`), tab selectors, and search bars that give the application a distinct, friendly, yet professional character.
3. **Restrained Visual Polish**: Avoids excessive decoration, AI-generated illustration clutter, or unnecessary iconography. Every visual element has a functional purpose.
4. **Consistent Elevation & Radius Hierarchy**: `rounded-full` for interactive actions/tabs, `rounded-2xl` (16px) for cards and modals, and `rounded-xl` (12px) for form inputs.

---

## 2. Typography System

The typography is built upon **Inter** (or **Plus Jakarta Sans**) with clean geometric sans-serif properties, crisp numerical tabular lining, and distinct hierarchy.

### Font Stack
```css
font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
```

### Type Scale & Hierarchy

| Role | Tailwind Utility | Size / Line Height | Weight | Color Token | Usage & Screen Placement |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Display / Hero Balance** | `text-4xl font-bold tracking-tight` | 36px / 44px | 700 (Bold) | `text-slate-900` | Large wallet balance figures (e.g. `₦238,000,909`) |
| **Page Title** | `text-2xl font-bold` | 24px / 32px | 700 (Bold) | `text-slate-900` | Top-level page headers (e.g. `Staff (45)`, `Inventory`) |
| **Section Header** | `text-lg font-semibold` | 18px / 28px | 600 (Semibold) | `text-slate-900` | Card titles, modal headers (e.g. `Create Departments`) |
| **Metric Stat Value** | `text-2xl font-bold` | 24px / 30px | 700 (Bold) | `text-slate-900` | Stat card counts (e.g. `2,345`, `10,000`) |
| **Table Header** | `text-xs font-semibold uppercase` | 12px / 16px | 600 (Semibold) | `text-slate-500` | Data table column labels (`NAME`, `DEPARTMENT`, `STATUS`) |
| **Body / Table Cell** | `text-sm font-normal` | 14px / 20px | 400 (Regular) | `text-slate-700` | General body copy, table cell content |
| **Body Emphasis** | `text-sm font-semibold` | 14px / 20px | 600 (Semibold) | `text-slate-900` | Contact names, highlighted monetary values |
| **Form Label** | `text-sm font-medium` | 14px / 20px | 500 (Medium) | `text-slate-800` | Form field labels |
| **Muted Caption / Tag** | `text-xs font-medium` | 12px / 16px | 500 (Medium) | `text-slate-500` | Timestamps, secondary subtitles, card captions |

---

## 3. Color Palette & Tokens (Tailwind CSS v4)

```css
@theme {
  --color-brand-50: #EFF6FF;
  --color-brand-100: #DBEAFE;
  --color-brand-200: #BFDBFE;
  --color-brand-500: #1D4ED8;
  --color-brand-600: #0B41CD; /* Primary Brand Blue */
  --color-brand-700: #0832A3; /* Hover State */
  --color-brand-800: #06257A; /* Active State */

  --color-canvas-bg: #F8FAFC;  /* Page Background */
  --color-surface: #FFFFFF;    /* Card & Modal Surface */
  
  --color-text-primary: #0F172A;   /* Slate 900 */
  --color-text-secondary: #334155; /* Slate 700 */
  --color-text-muted: #64748B;     /* Slate 500 */
  --color-text-subtle: #94A3B8;    /* Slate 400 */

  --color-border-primary: #E2E8F0; /* Slate 200 */
  --color-border-subtle: #F1F5F9;  /* Slate 100 */

  --color-status-success: #10B981;       /* Emerald 500 */
  --color-status-success-bg: #ECFDF5;    /* Emerald 50 */
  --color-status-danger: #EF4444;        /* Red 500 */
  --color-status-danger-bg: #FEF2F2;     /* Red 50 */
  --color-status-warning: #F59E0B;       /* Amber 500 */
  --color-status-warning-bg: #FFFBEB;    /* Amber 50 */
  --color-status-info: #3B82F6;          /* Blue 500 */
  --color-status-info-bg: #EFF6FF;       /* Blue 50 */
}
```

---

## 4. Spacing, Grid & Layout Metrics

The layout adheres to an **8px base grid system**:

- **Page Container Padding**: `px-6 py-6` (24px) or `px-8 py-8` (32px) on desktop; `px-4 py-4` (16px) on mobile.
- **Card Padding**: `p-6` (24px) for standard content cards; `p-4` (16px) for compact stat widgets.
- **Form Spacing**: `space-y-4` (16px) between standard input groups; `space-y-6` (24px) between form sections.
- **Grid Gaps**:
  - Executive stat cards: `grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4`
  - Dashboard panels: `grid grid-cols-1 lg:grid-cols-2 gap-6`
  - Form two-column layouts: `grid grid-cols-1 sm:grid-cols-2 gap-4`

---

## 5. Borders, Radii & Shadow Elevations

| UI Element | Radius Class | Pixel Equivalent | Border Utility | Shadow Utility |
| :--- | :--- | :--- | :--- | :--- |
| **Action & Pill Buttons** | `rounded-full` | 9999px | None (or `border border-slate-200`) | `shadow-sm` / `shadow-blue-500/20` |
| **Topbar Navigation Pills**| `rounded-full` | 9999px | `border border-slate-200` | `shadow-none` |
| **Search Bars** | `rounded-full` | 9999px | `border border-slate-200` | `shadow-none` |
| **Cards & Panels** | `rounded-2xl` | 16px | `border border-slate-100` | `shadow-[0_2px_12px_rgba(0,0,0,0.04)]` |
| **Modals & Dialogs** | `rounded-2xl` | 16px | `border border-slate-100` | `shadow-2xl` |
| **Form Inputs & Selects** | `rounded-xl` | 12px | `border border-slate-200` | `focus:ring-2 focus:ring-blue-500` |
| **Badges & Status Tags** | `rounded-full` | 9999px | None | None |
| **Split Metric Badges** | `rounded-xl` | 12px | None | `shadow-sm` |

---

## 6. Component Catalog & Implementation Specifications

### 6.1 Topbar Navigation System
As observed in `Dashboard.png`, `Staff.png`, and `Payroll(Schedule.png`:
- **Brand Identity**: CebizPay logo in a clean rounded container on the left.
- **User Greeting Pill**: `Hello [FirstName]` with an avatar circle (`h-8 w-8 rounded-full`).
- **Navigation Pills**:
  - **Inactive Pill**: `rounded-full bg-white border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 inline-flex items-center gap-2`
  - **Active Pill**: `rounded-full bg-[#0B41CD] px-5 py-2 text-sm font-medium text-white shadow-sm inline-flex items-center gap-2`
- **Notification Bell**: Circular icon button (`h-10 w-10 rounded-full bg-white border border-slate-200 flex items-center justify-center text-slate-600 hover:bg-slate-50`).

### 6.2 Data Tables & Row Presentation
- **Header Row**: Transparent background with uppercase muted text (`text-xs font-semibold text-slate-500 tracking-wider py-3`).
- **Body Rows**:
  - White background, hover effect (`hover:bg-slate-50/70 transition-colors`).
  - Dividers: Subtle border bottom (`border-b border-slate-100`).
  - Row padding: `py-4 px-3`.
  - Action column: "View" primary blue pill button (`px-4 py-1.5 bg-[#0B41CD] text-white text-xs font-medium rounded-full`).
- **Pagination Footer**:
  - Left: "Next" pill button (`px-4 py-1.5 border border-slate-200 text-xs font-medium rounded-full`).
  - Right: Page navigation controls (`< [1] > of [TotalPages]`).

### 6.3 Split Metric Filter Badges (e.g. Invoicing)
As observed in `Invoice generator.png`:
- Container with two connected segments:
  - Left segment (Active/Label): `bg-[#0B41CD] text-white px-4 py-2 text-xs font-medium rounded-l-xl`
  - Right segment (Count): `bg-blue-100 text-[#0B41CD] px-3 py-2 text-xs font-bold rounded-r-xl`

### 6.4 Form Controls & Inputs
- **Text / Number / Email Inputs**:
  ```jsx
  <input className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-[#0B41CD] focus:border-transparent transition-all" />
  ```
- **Search Input**:
  ```jsx
  <div className="relative">
    <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
    <input placeholder="Search" className="pl-10 pr-4 py-2 rounded-full border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#0B41CD]" />
  </div>
  ```

### 6.5 Feedback & Modals
- **Centered Modal Dialog**:
  - Backdrop: `fixed inset-0 bg-slate-900/40 backdrop-blur-xs flex items-center justify-center p-4 z-50`
  - Modal Box: `bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl relative border border-slate-100`
  - Close button: Top right `x` icon (`text-slate-400 hover:text-slate-700`).
  - Success State (`done.png` pattern): Large green/blue check circle, bold title ("Successful"), supporting text, and "Done" full-width CTA.

---

## 7. Iconography System & Guidelines

### Recommended Icon Library: **Lucide React** (`lucide-react`)

**Rationale**:
- Lucide React matches the exact outline stroke style (1.5px - 2px stroke weight) observed throughout the 479 design library screens.
- Zero-bloat, tree-shakeable, clean React implementation.

### Icon Sizing Tiers
- **Micro (14px - 16px / `h-3.5 w-3.5` - `h-4 w-4`)**: Table row actions, dropdown chevrons, input prefix icons, status badges.
- **Standard (18px - 20px / `h-4.5 w-4.5` - `h-5 w-5`)**: Navigation pill icons, button prefix icons, toast icons.
- **Large (24px / `h-6 w-6`)**: Section header icons, stat card icon circles.
- **Hero / Feedback (40px - 48px / `h-10 w-10` - `h-12 w-12`)**: Success/Error modal illustrations and empty state icons.

### Strict Iconography Rules
1. **No Decorative Clutter**: Do not add icons to table headers, plain buttons, or form labels unless specifically indicated by the design reference.
2. **Stroke Consistency**: Use `strokeWidth={1.75}` consistently across all standard icons.
3. **Color Matching**: Icons inside active pills inherit `text-white`; icons in standard buttons inherit `text-slate-600`.

---

## 8. Responsive Design & Breakpoint Strategy

| Breakpoint | Width Range | Layout Adaptation Rules |
| :--- | :--- | :--- |
| **Desktop (`lg`, `xl`)** | `>= 1024px` | Full horizontal topbar with all navigation pills visible, multi-column dashboard grids (6-column stats), full multi-column data tables, side-by-side forms. |
| **Tablet (`md`)** | `768px - 1023px` | Horizontally scrollable topbar navigation pills (`overflow-x-auto no-scrollbar`), 3-column stat grids, collapsible ERP sidebar drawer, horizontal scroll enabled for wide data tables. |
| **Mobile (`sm`)** | `< 768px` | Topbar collapses into a sticky header with Brand Logo, User Avatar, and Hamburger Menu trigger; 2-column stat cards; data tables convert to responsive stacked card lists; modals convert to slide-up bottom sheets (`rounded-t-2xl`). |
