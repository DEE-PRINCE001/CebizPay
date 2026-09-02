// Nigerian Commercial Banks list for NUBAN account transfers
export const NIGERIAN_BANKS = [
  { code: "058", name: "GTBank (Guaranty Trust Bank)" },
  { code: "044", name: "Access Bank" },
  { code: "057", name: "Zenith Bank" },
  { code: "011", name: "First Bank of Nigeria" },
  { code: "033", name: "United Bank for Africa (UBA)" },
  { code: "035", name: "Wema Bank" },
  { code: "232", name: "Sterling Bank" },
  { code: "070", name: "Fidelity Bank" },
  { code: "214", name: "First City Monument Bank (FCMB)" },
  { code: "076", name: "Polaris Bank" },
  { code: "221", name: "Stanbic IBTC Bank" },
  { code: "032", name: "Union Bank of Nigeria" },
  { code: "101", name: "Providus Bank" },
  { code: "100", name: "SunTrust Bank" },
  { code: "082", name: "Keystone Bank" },
  { code: "090110", name: "VFD Microfinance Bank" },
  { code: "090267", name: "Kuda Bank" },
  { code: "090405", name: "Moniepoint MFB" },
  { code: "090551", name: "FairMoney MFB" },
  { code: "090325", name: "Sparkle Bank" },
  { code: "090175", name: "Rubies MFB" },
  { code: "090115", name: "TCF MFB" }
];

// VAS Telecommunication Operators
export const VAS_NETWORKS = [
  { id: "MTN", name: "MTN Nigeria", color: "bg-amber-400 text-amber-950", border: "border-amber-400" },
  { id: "AIRTEL", name: "Airtel Nigeria", color: "bg-red-500 text-white", border: "border-red-500" },
  { id: "GLO", name: "Globacom (Glo)", color: "bg-emerald-600 text-white", border: "border-emerald-600" },
  { id: "9MOBILE", name: "9mobile", color: "bg-lime-600 text-white", border: "border-lime-600" }
];

// Document Types for Individual KYC
export const KYC_DOCUMENT_TYPES = [
  { id: 1, name: "National ID (NIMC)", code: "NIMC_CARD" },
  { id: 2, name: "Driver's License", code: "DRIVERS_LICENSE" },
  { id: 3, name: "International Passport", code: "INTERNATIONAL_PASSPORT" },
  { id: 4, name: "Voter's Card", code: "VOTERS_CARD" }
];

// Currencies supported
export const CURRENCIES = [
  { code: "NGN", symbol: "₦", name: "Nigerian Naira (Primary)" },
  { code: "INTERNATIONAL_NGN", symbol: "₦", name: "International NGN (Contractors)" },
  { code: "USDT", symbol: "₮", name: "Tether (USDT)" },
  { code: "USD", symbol: "$", name: "US Dollar (ERP Reporting)" },
  { code: "EUR", symbol: "€", name: "Euro (ERP Reporting)" },
  { code: "GBP", symbol: "£", name: "British Pound (ERP Reporting)" },
  { code: "GHS", symbol: "GH₵", name: "Ghanaian Cedi (ERP Reporting)" }
];

// Seed & Demo Accounts for quick test exploration
export const DEMO_USERS = [
  {
    role: "Super Admin",
    name: "Honour Ajani (Platform Operator)",
    email: "honour@gmail.com",
    password: "CephHonSec.123tryit",
    description: "Full platform control plane, compliance authority, fee engine, and reconciliation.",
    badge: "bg-purple-100 text-purple-800 border-purple-200"
  },
  {
    role: "Organization CEO / Admin",
    name: "Apex Global Tech Corp",
    email: "org@apextech.com",
    password: "Password@123",
    description: "Corporate treasury, workforce management, payroll engine, and full ERP suite.",
    badge: "bg-blue-100 text-blue-800 border-blue-200"
  },
  {
    role: "Staff / Consumer User",
    name: "Amina Adeleke",
    email: "amina.adeleke@example.com",
    password: "Password@123",
    phone: "08012345678",
    description: "Personal wallet, workplace payslips, salary loans, savings, and peer thrift.",
    badge: "bg-emerald-100 text-emerald-800 border-emerald-200"
  }
];
