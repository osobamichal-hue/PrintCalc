"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson } from "@/lib/api";
import { btnPrimary, btnSecondary, inputClass, labelClass } from "@/lib/ui";

type LineDraft = {
  description: string;
  quantity: string;
  unit: string;
  unitPrice: string;
  taxRatePercent: string;
  lineTotal: string;
};

const emptyLine = (): LineDraft => ({
  description: "",
  quantity: "1",
  unit: "ks",
  unitPrice: "0",
  taxRatePercent: "21",
  lineTotal: "0",
});

export default function NewPurchaseInvoicePage() {
  const router = useRouter();
  const [number, setNumber] = useState("");
  const [issueDate, setIssueDate] = useState(new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState("");
  const [supplierName, setSupplierName] = useState("");
  const [supplierIco, setSupplierIco] = useState("");
  const [supplierDic, setSupplierDic] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<LineDraft[]>([emptyLine()]);
  const [msg, setMsg] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function updateLine(i: number, patch: Partial<LineDraft>) {
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  }

  async function submit() {
    setMsg(null);
    setBusy(true);
    try {
      const body = {
        number,
        issueDate: new Date(issueDate).toISOString(),
        dueDate: dueDate ? new Date(dueDate).toISOString() : null,
        supplierName,
        supplierCompanyId: supplierIco || null,
        supplierVatId: supplierDic || null,
        notes: notes || null,
        lines: lines
          .filter((l) => l.description.trim())
          .map((l) => {
            const qty = parseFloat(l.quantity.replace(",", ".")) || 1;
            const unitPrice = parseFloat(l.unitPrice.replace(",", ".")) || 0;
            const lineTotal = parseFloat(l.lineTotal.replace(",", ".")) || qty * unitPrice;
            return {
              description: l.description.trim(),
              quantity: qty,
              unit: l.unit || "ks",
              unitPrice,
              taxRatePercent: parseFloat(l.taxRatePercent.replace(",", ".")) || 21,
              lineTotal,
              productCode: null,
              ean: null,
            };
          }),
      };
      const created = await apiJson<{ id: number }>("/api/purchase-invoices", {
        method: "POST",
        body: JSON.stringify(body),
      });
      router.push(`/purchase-invoices/${created.id}`);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Nová přijatá faktura" description="Ruční zadání hlavičky a řádků." />
      {msg && <StatusBanner message={msg} />}

      <div className="grid gap-4 rounded-lg border border-zinc-200 p-4 dark:border-zinc-800 md:grid-cols-2">
        <label className={labelClass}>
          Číslo FA
          <input className={inputClass} value={number} onChange={(e) => setNumber(e.target.value)} />
        </label>
        <label className={labelClass}>
          Dodavatel
          <input className={inputClass} value={supplierName} onChange={(e) => setSupplierName(e.target.value)} />
        </label>
        <label className={labelClass}>
          Datum vystavení
          <input type="date" className={inputClass} value={issueDate} onChange={(e) => setIssueDate(e.target.value)} />
        </label>
        <label className={labelClass}>
          Datum splatnosti
          <input type="date" className={inputClass} value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
        </label>
        <label className={labelClass}>
          IČO
          <input className={inputClass} value={supplierIco} onChange={(e) => setSupplierIco(e.target.value)} />
        </label>
        <label className={labelClass}>
          DIČ
          <input className={inputClass} value={supplierDic} onChange={(e) => setSupplierDic(e.target.value)} />
        </label>
        <label className={`${labelClass} md:col-span-2`}>
          Poznámka
          <input className={inputClass} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </label>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium">Řádky</h2>
          <button type="button" className={btnSecondary} onClick={() => setLines((l) => [...l, emptyLine()])}>
            + Řádek
          </button>
        </div>
        {lines.map((line, i) => (
          <div key={i} className="grid gap-2 rounded border border-zinc-200 p-3 dark:border-zinc-800 md:grid-cols-6">
            <label className={`${labelClass} md:col-span-2`}>
              Popis
              <input className={inputClass} value={line.description} onChange={(e) => updateLine(i, { description: e.target.value })} />
            </label>
            <label className={labelClass}>
              Množství
              <input className={inputClass} value={line.quantity} onChange={(e) => updateLine(i, { quantity: e.target.value })} />
            </label>
            <label className={labelClass}>
              MJ
              <input className={inputClass} value={line.unit} onChange={(e) => updateLine(i, { unit: e.target.value })} />
            </label>
            <label className={labelClass}>
              Cena/j.
              <input className={inputClass} value={line.unitPrice} onChange={(e) => updateLine(i, { unitPrice: e.target.value })} />
            </label>
            <label className={labelClass}>
              Celkem
              <input className={inputClass} value={line.lineTotal} onChange={(e) => updateLine(i, { lineTotal: e.target.value })} />
            </label>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <button type="button" className={btnPrimary} disabled={busy} onClick={() => void submit()}>
          Uložit a pokračovat
        </button>
        <Link href="/purchase-invoices" className={btnSecondary}>
          Zpět
        </Link>
      </div>
    </div>
  );
}
