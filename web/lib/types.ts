export type Customer = {
  id: number;
  name: string;
  companyId: string | null;
  vatId: string | null;
  street: string | null;
  city: string | null;
  zip: string | null;
  email: string | null;
  phone: string | null;
  invoiceDueDays: number | null;
  preferredPaymentMethod: string | null;
  createdAt: string;
};

export type CustomerWrite = {
  name: string;
  companyId?: string | null;
  vatId?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  email?: string | null;
  phone?: string | null;
  invoiceDueDays?: number | null;
  preferredPaymentMethod?: string | null;
};

export type Lookups = {
  customers: { id: number; name: string }[];
  filamentTypes: {
    id: number;
    name: string;
    manufacturer: string | null;
    averagePricePerKg: number;
    diameterMm: number;
    color: string | null;
  }[];
  printers: {
    id: number;
    name: string;
    kind: string;
    hourlyRate: number;
    kwhPerHour: number;
    startFeePerPrint: number;
  }[];
  printModels: {
    id: number;
    name: string;
    fileType: string;
    originalFileName: string;
    estimatedMaterialGrams: number | null;
    estimatedPrintHours: number | null;
    createdAt: string;
  }[];
};

export type AppSettingRow = { key: string; value: string };
