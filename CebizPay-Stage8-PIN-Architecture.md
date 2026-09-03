# CebizPay — Stage 8 (Revised): Global Transaction PIN Architecture

> **Replaces** the Stage 8 section in `CebizPay-Frontend-Implementation-Plan.md`  
> **PRD Reference:** §4.1 — "A 4-digit Transaction PIN must be collected as a standalone authorization step before any outbound financial mutation on every surface."

---

## The Design: Promise-Based Global PIN Modal

Instead of embedding `PinInput` inside every individual modal, a **single standalone overlay** intercepts all financial mutations. Any component that needs PIN authorization calls a single function from context and awaits the result — exactly like how modern banking apps work (the PIN screen pops over whatever is already open).

### How it works

```
User clicks "Send Transfer"
  → QuickTransferModal validates form fields
  → Calls:  const pin = await requestPin({ title: 'Authorize Transfer', subtitle: '₦50,000 to Zenith Bank' })
  → TransactionPinModal slides in over the transfer modal
  → User enters 4 digits
  → User presses "Confirm"
  → requestPin() resolves with "1234"
  → QuickTransferModal uses the resolved PIN in its API call
  → If user presses "Cancel" → requestPin() rejects → modal catches error and does nothing
```

This means:
- PIN logic is written **once**, not duplicated across 10+ modals
- PIN modal UI is **consistent** everywhere
- Lockout error handling is **centralized** in one place
- Adding PIN to a new form in the future requires **one line of code**

---

## Task 8.0 — Create `TransactionPinContext`

**New File:** `frontend/src/context/TransactionPinContext.jsx`  
**Pattern:** Mirrors `ToastContext.jsx` — a provider that renders its UI at the root and exposes a callable function to all descendants.

### Full implementation spec:

```jsx
/**
 * TransactionPinContext
 *
 * Provides a Promise-based requestPin() function that any component can
 * call to collect a 4-digit Transaction PIN from the user via a standalone
 * overlay modal.
 *
 * Usage:
 *   const { requestPin } = useTransactionPin();
 *   try {
 *     const pin = await requestPin({ title: 'Authorize Payroll', subtitle: '₦2.4M to 48 staff' });
 *     // pin is a 4-character string e.g. "1234"
 *     await apiClient.postFinancial('/org/payroll/execute', { ...data, transactionPin: pin });
 *   } catch {
 *     // user cancelled — do nothing
 *   }
 */

import React, { createContext, useContext, useState, useCallback, useRef } from 'react';

export const TransactionPinContext = createContext(null);

export function TransactionPinProvider({ children }) {
  // Holds the current pending PIN request
  const [pinRequest, setPinRequest] = useState(null);
  // pinRequest shape: { title, subtitle, resolve, reject }

  const resolverRef = useRef(null);

  /**
   * requestPin({ title, subtitle })
   * Returns a Promise<string> that resolves with the 4-digit PIN,
   * or rejects if the user cancels.
   */
  const requestPin = useCallback(({ title = 'Authorize Transaction', subtitle = '' } = {}) => {
    return new Promise((resolve, reject) => {
      resolverRef.current = { resolve, reject };
      setPinRequest({ title, subtitle, resolve, reject });
    });
  }, []);

  const handleConfirm = useCallback((pin) => {
    resolverRef.current?.resolve(pin);
    setPinRequest(null);
    resolverRef.current = null;
  }, []);

  const handleCancel = useCallback(() => {
    resolverRef.current?.reject(new Error('PIN entry cancelled by user.'));
    setPinRequest(null);
    resolverRef.current = null;
  }, []);

  return (
    <TransactionPinContext.Provider value={{ requestPin }}>
      {children}
      {/* Global PIN Modal — rendered at root, above all other modals */}
      {pinRequest && (
        <TransactionPinModal
          title={pinRequest.title}
          subtitle={pinRequest.subtitle}
          onConfirm={handleConfirm}
          onCancel={handleCancel}
        />
      )}
    </TransactionPinContext.Provider>
  );
}

export function useTransactionPin() {
  const context = useContext(TransactionPinContext);
  if (!context) {
    throw new Error('useTransactionPin must be used within a TransactionPinProvider');
  }
  return context;
}
```

**Key design decisions:**
- Uses a `useRef` to hold the resolver so it is never stale inside `handleConfirm`/`handleCancel`
- The modal renders as a sibling to `{children}` inside the provider, so it is always above everything
- `requestPin` returns a plain Promise — no special hook required at the call site beyond `await`
- The context exposes only `requestPin` — internal state is private to the provider

**Register in `App.jsx`:** Wrap the app with `<TransactionPinProvider>` as a sibling to `<ToastProvider>` and `<AuthProvider>`, at the root level.

---

## Task 8.1 — Create `TransactionPinModal` Component

**New File:** `frontend/src/components/feedback/TransactionPinModal.jsx`  
**Note:** This component is internal to `TransactionPinContext.jsx` but kept in a separate file for maintainability. Import it into the context file.

### Full implementation spec:

```jsx
/**
 * TransactionPinModal
 *
 * Standalone full-screen overlay that collects a 4-digit Transaction PIN.
 * Rendered by TransactionPinContext — not used directly by any other component.
 *
 * Props:
 *   title    — e.g. "Authorize Payroll Disbursement"
 *   subtitle — e.g. "₦2.4M to 48 staff members"
 *   onConfirm(pin: string) — called when user enters full PIN and presses Confirm
 *   onCancel()             — called when user presses Cancel or presses Escape
 */
```

**Visual design spec:**
- **Backdrop:** `fixed inset-0 bg-black/50 z-[9999] flex items-center justify-center` — highest z-index in the app, appears above all other modals
- **Card:** `bg-white rounded-3xl shadow-2xl p-8 w-full max-w-sm mx-4 space-y-6`
- **Icon:** A centered `ShieldCheck` icon (brand color) at the top
- **Title:** Bold, centered, text-slate-900
- **Subtitle:** Small, centered, text-slate-500 — shows the operation context (e.g. the amount and recipient)
- **`PinInput` component** (existing): centered, reuse the existing component from `frontend/src/components/forms/PinInput.jsx`
- **Error Alert** (inline): shown below PinInput if an API error is returned via the `error` prop — this handles wrong PIN, lockout errors, etc.
- **Buttons:**
  - "Confirm" — `variant="primary"`, full width, disabled until `pin.length === 4`, shows loading spinner while the parent is processing
  - "Cancel" — `variant="ghost"`, calls `onCancel()`
- **Keyboard:** pressing `Escape` calls `onCancel()`

**Internal state:**
- `pin` — string, starts empty, updated by PinInput
- `loading` — boolean, set to `true` when Confirm is pressed, reset if an error comes back
- `error` — string | null — error message to show inline

**Important:** The modal does **not** make the API call itself. It only collects the PIN and returns it. Error feedback is handled externally — the calling modal sets an error and calls `requestPin()` again if needed. The TransactionPinModal itself auto-clears and closes after `onConfirm` is called.

**On `onConfirm` click:**
1. Set `loading = true`
2. Call `onConfirm(pin)` — the parent's Promise resolves
3. The context removes the modal from the DOM
4. No need to reset state (the component unmounts)

**Acceptance Criteria:**
- Modal appears above all other content (z-index above other modals)
- PIN digits auto-advance focus (reusing PinInput behaviour)
- Escape key triggers cancel
- Confirm disabled until all 4 digits entered
- Component has no API dependency — purely presentational + PIN collection

---

## Task 8.2 — Remove Embedded `PinInput` from `RunPayrollWizardModal`

**File:** `frontend/src/components/payroll/RunPayrollWizardModal.jsx`  
**Change:** Step 3 currently renders `<PinInput>` inline. Replace it with a `requestPin()` call.

### What changes:

**Remove:**
- `const [pin, setPin] = useState('')` state
- The entire Step 3 JSX block (the PIN step UI inside the modal)
- The "Proceed to PIN" button in Step 2 (no longer needed)
- The `step === 3` branch rendering
- Import of `PinInput`
- The `step` state can be simplified from 3 → 2 steps (Period Selection → Review → Execute directly)

**Add:**
```jsx
import { useTransactionPin } from '../../context/TransactionPinContext';

// Inside component:
const { requestPin } = useTransactionPin();

// The Execute handler (was triggered from Step 3, now triggered from Step 2 "Confirm"):
const handleExecute = async () => {
  setExecuting(true);
  setError(null);

  try {
    // Request PIN — this suspends here until user enters PIN or cancels
    const pin = await requestPin({
      title: 'Authorize Payroll Disbursement',
      subtitle: `${formatCurrency(calculationResult?.totalNetPay || 0)} to ${calculationResult?.staffCount || 0} staff members`,
    });

    // PIN received — proceed with financial mutation
    const response = await apiClient.postFinancial('/org/payroll/execute', {
      currency,
      periodStart: new Date(periodStart).toISOString(),
      periodEnd: new Date(periodEnd).toISOString(),
      transactionPin: pin,
      criteria: {},
    });

    // Handle success...
  } catch (err) {
    if (err.message === 'PIN entry cancelled by user.') {
      // User cancelled — silently do nothing
      setExecuting(false);
      return;
    }
    const parsed = err.problemDetails || parseProblemDetails(err);
    setError(parsed.message || 'Payroll execution failed.');
  } finally {
    setExecuting(false);
  }
};
```

**Simplified wizard flow:** Step 1 (Period Selection) → Step 2 (Review Calculation + "Confirm & Disburse" button) → PIN Modal appears → on PIN confirm, execution proceeds.

**Acceptance Criteria:**
- Wizard has 2 visible steps (not 3)
- PIN modal appears as a separate overlay when "Confirm & Disburse" is clicked
- Cancelling the PIN modal returns user to the Review step with no error
- Wrong PIN error from the backend surfaces correctly

---

## Task 8.3 — Remove Embedded `PinInput` from `QuickTransferModal`

**File:** `frontend/src/components/dashboard/QuickTransferModal.jsx`  
**Change:** The modal currently renders `<PinInput label="Authorize with 4-Digit PIN" ...>` inline in the form. Replace with `requestPin()`.

### What changes:

**Remove:**
- `const [pin, setPin] = useState('')` state
- The `<PinInput>` JSX block from the form
- The `if (pin.length < 4)` validation check (PIN is now guaranteed 4 digits by the modal)
- Import of `PinInput`

**Add:**
```jsx
import { useTransactionPin } from '../../context/TransactionPinContext';

const { requestPin } = useTransactionPin();

const handleSubmit = async (e) => {
  e.preventDefault();
  const numAmount = parseFloat(amount);

  if (!numAmount || numAmount <= 0) {
    setError('Please enter a valid transfer amount greater than zero.');
    return;
  }
  // ... other field validations ...

  setLoading(true);
  setError(null);

  try {
    // Suspend here — PIN modal appears over the transfer modal
    const pin = await requestPin({
      title: transferType === 'peer' ? 'Authorize Peer Transfer' : 'Authorize Bank Transfer',
      subtitle: `₦${parseFloat(amount).toLocaleString()} to ${
        transferType === 'peer' ? recipient : (resolvedAccountName || accountNumber)
      }`,
    });

    // Proceed with transfer
    const result = await apiClient.postFinancial(
      transferType === 'peer' ? '/wallet/transfer/peer' : '/wallet/transfer/bank',
      {
        ...(transferType === 'peer'
          ? { recipientIdentifier: recipient.trim() }
          : { destinationBankCode: bankCode, destinationAccountNumber: accountNumber }),
        amount: numAmount,
        currency: 'NGN',
        transactionPin: pin,
      }
    );
    // Handle success...
  } catch (err) {
    if (err.message === 'PIN entry cancelled by user.') {
      setLoading(false);
      return;
    }
    const parsed = err.problemDetails || parseProblemDetails(err);
    setError(parsed.message || 'Transfer failed.');
  } finally {
    setLoading(false);
  }
};
```

**The transfer form is now cleaner:** user fills in recipient + amount, clicks "Send Transfer" → PIN modal pops separately → on confirm, transfer executes.

**Acceptance Criteria:**
- Transfer modal no longer contains a PIN input field
- PIN modal appears when "Send Transfer" is clicked (after form validation passes)
- Cancelling PIN returns the user to the filled transfer form (their inputs are preserved)
- Wrong PIN backend error handled correctly

---

## Task 8.4 — Add PIN to `WithdrawSavingsModal`

**File:** `frontend/src/components/savings/WithdrawSavingsModal.jsx`

```jsx
const { requestPin } = useTransactionPin();

const handleWithdraw = async () => {
  // form validation first...
  try {
    const pin = await requestPin({
      title: 'Authorize Savings Withdrawal',
      subtitle: `${formatCurrency(amount)} from ${plan?.name || 'savings plan'}`,
    });

    await apiClient.postFinancial(`/work/savings/${plan.id}/withdraw`, {
      amount: parseFloat(amount),
      transactionPin: pin,
    });
    // success...
  } catch (err) {
    if (err.message === 'PIN entry cancelled by user.') return;
    // handle API error...
  }
};
```

**Acceptance Criteria:**
- PIN modal appears when withdrawal is confirmed
- Cancelling does not submit the withdrawal

---

## Task 8.5 — Add PIN to All 4 VAS Forms

**Files:** `AirtimeForm.jsx`, `DataBundleForm.jsx`, `ElectricityForm.jsx`, `CableTvForm.jsx`

Each VAS form's submit handler follows the same pattern:

```jsx
const { requestPin } = useTransactionPin();

const handleSubmit = async (e) => {
  e.preventDefault();
  // field validations...

  try {
    const pin = await requestPin({
      title: 'Authorize Purchase',
      subtitle: `${serviceName} — ₦${amount}`,  // e.g. "Airtime Top-up — ₦500"
    });

    await apiClient.postFinancial('/vas/airtime', {   // or /vas/data, /vas/electricity, /vas/cable
      // ...form fields...
      transactionPin: pin,
    });
    // success...
  } catch (err) {
    if (err.message === 'PIN entry cancelled by user.') return;
    // handle API error...
  }
};
```

**Acceptance Criteria:**
- All 4 VAS forms trigger the standalone PIN modal before purchase
- Each PIN modal subtitle describes the specific purchase (e.g. "MTN Airtime — ₦500")
- Cancelling PIN does not submit the purchase

---

## Task 8.6 — Add PIN to `CreateVoucherModal` (Stage 4.3)

When building the new voucher creation modal in Stage 4, use `requestPin()` instead of embedding PinInput:

```jsx
const pin = await requestPin({
  title: 'Authorize Payment Voucher',
  subtitle: `₦${amount} to ${recipientName}`,
});
```

---

## Task 8.7 — Register `TransactionPinProvider` in `App.jsx`

**File:** `frontend/src/App.jsx`

Wrap the app tree — place `TransactionPinProvider` inside `AuthProvider` and `OrgProvider`, alongside `ToastProvider`:

```jsx
<AuthProvider>
  <OrgProvider>
    <ToastProvider>
      <TransactionPinProvider>
        <RouterProvider ... />
      </TransactionPinProvider>
    </ToastProvider>
  </OrgProvider>
</AuthProvider>
```

**Acceptance Criteria:**
- `useTransactionPin()` is callable from any component anywhere in the app
- `TransactionPinModal` is rendered at the root level
- Only one PIN modal is ever shown at a time

---

## Summary of Changes from Original Stage 8

| Item | Original Plan | Revised Plan |
|---|---|---|
| PIN location | Embedded `PinInput` in each modal | Standalone `TransactionPinModal` overlay |
| PIN architecture | Duplicated per-modal state | Single `TransactionPinContext` + `requestPin()` |
| Wizard step count (Payroll) | 3 steps (step 3 = PIN) | 2 steps (PIN is separate overlay) |
| Transfer form | Contains PIN input field inline | No PIN in form; PIN overlay pops after submit |
| Adding PIN to future features | Copy-paste PinInput + state | One line: `await requestPin({ title, subtitle })` |
| Lockout handling | Duplicated across every modal | One place: parse lockout in `requestPin()` error flow |
| Cancel behaviour | Varies per modal | Consistent: promise rejects, calling modal does nothing |

---

## Files Created / Modified in Revised Stage 8

| Action | File |
|---|---|
| 🆕 Create | `frontend/src/context/TransactionPinContext.jsx` |
| 🆕 Create | `frontend/src/components/feedback/TransactionPinModal.jsx` |
| ✏️ Modify | `frontend/src/components/payroll/RunPayrollWizardModal.jsx` |
| ✏️ Modify | `frontend/src/components/dashboard/QuickTransferModal.jsx` |
| ✏️ Modify | `frontend/src/components/savings/WithdrawSavingsModal.jsx` |
| ✏️ Modify | `frontend/src/components/vas/AirtimeForm.jsx` |
| ✏️ Modify | `frontend/src/components/vas/DataBundleForm.jsx` |
| ✏️ Modify | `frontend/src/components/vas/ElectricityForm.jsx` |
| ✏️ Modify | `frontend/src/components/vas/CableTvForm.jsx` |
| ✏️ Modify | `frontend/src/App.jsx` (register provider) |

All other stages in the main plan remain unchanged.
