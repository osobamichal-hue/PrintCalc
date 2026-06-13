"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, downloadUrl } from "@/lib/api";
import { btnPrimary, btnSecondary, inputClass, tableBody, tableHead, tableWrap } from "@/lib/ui";

const STATUS: Record<string, string> = {
  Draft: "Koncept",
  ReadyToMatch: "K párování",
  Matched: "Spárováno",
  Posted: "Na skladě",
  Cancelled: "Zrušeno",
};

const MATCH: Record<string, string> = {
  Unmatched: "Nespárováno",
  Suggested: "Návrh",
  AutoMatched: "Auto",
  ManualMatched: "Ručně",
};

type FilamentType = { id: number; name: string };

type Line = {
  id: number;
  description: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  lineTotal: number;
  filamentTypeId: number | null;
  filamentTypeName: string | null;
  matchStatus: string;
  matchConfidence: number;
  weightKg: number;
  pricePerKg: number;
  pieceCount: number;
};

type Invoice = {
  id: number;
  number: string;
  issueDate: string;
  dueDate: string | null;
  supplierName: string;
  supplierCompanyId: string | null;
  supplierVatId: string | null;
  totalAmount: number;
  status: string;
  importSource: string;
  sourceFileName: string | null;
  notes: string | null;
  lines: Line[];
};

export default function PurchaseInvoiceDetailPage({ params }: { params: { id: string } }) {
  const id = Number(params.id);
  const [inv, setInv] = useState<Invoice | null>(null);
  const [types, setTypes] = useState<FilamentType[]>([]);
  const [msg, setMsg] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [step, setStep] = useState<1 | 2 | 3>(2);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      const [detail, filamentTypes] = await Promise.all([
        apiJson<Invoice>(`/api/purchase-invoices/${id}`),
        apiJson<FilamentType[]>("/api/filament-types"),
      ]);
      setInv(detail);
      setTypes(filamentTypes);
      if (detail.status === "Posted") setStep(3);
      else if (detail.lines.every((l) => l.filamentTypeId)) setStep(3);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function runMatch() {
    setBusy(true);
    setMsg(null);
    try {
      const updated = await apiJson<Invoice>(`/api/purchase-invoices/${id}/match`, { method: "POST" });
      setInv(updated);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    } finally {
      setBusy(false);
    }
  }

  async function manualMatch(lineId: number, filamentTypeId: number) {
    setMsg(null);
    try {
      const updated = await apiJson<Invoice>(`/api/purchase-invoices/${id}/lines/${lineId}/match`, {
        method: "PUT",
        body: JSON.stringify({ filamentTypeId }),
      });
      setInv(updated);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function createType(lineId: number) {
    setMsg(null);
    try {
      const res = await apiJson<{ invoice: Invoice }>(
        `/api/purchase-invoices/${id}/lines/${lineId}/create-filament-type`,
        { method: "POST" }
      );
      setInv(res.invoice);
      const filamentTypes = await apiJson<FilamentType[]>("/api/filament-types");
      setTypes(filamentTypes);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function postToStock() {
    if (!confirm("Zaúčtovat všechny řádky na sklad?")) return;
    setBusy(true);
    setMsg(null);
    try {
      const updated = await apiJson<Invoice>(`/api/purchase-invoices/${id}/post-to-stock`, { method: "POST" });
      setInv(updated);
      setStep(3);
      setMsg("Faktura byla zaúčtována na sklad.");
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    } finally {
      setBusy(false);
    }
  }

  if (!inv) {
    return (
      <div className="space-y-4">
        {msg ? <StatusBanner message={msg} /> : <p className="text-sm text-zinc-500">Načítám…</p>}
      </div>
    );
  }

  const allMatched = inv.lines.every((l) => l.filamentTypeId);
  const readOnly = inv.status === "Posted";

  return (
    <div className="space-y-4">
      <PageHeader
        title={`FA ${inv.number}`}
        description={`${inv.supplierName} · ${STATUS[inv.status] ?? inv.status}`}
      >
        {inv.sourceFileName && (
          <a href={downloadUrl(`/api/purchase-invoices/${id}/source-file`)} className={btnSecondary}>
            Stáhnout originál
          </a>
        )}
        <Link href="/purchase-invoices" className={btnSecondary}>
          Seznam
        </Link>
      </PageHeader>
      {msg && <StatusBanner message={msg} />}

      <div className="flex flex-wrap gap-2 text-sm">
        {[1, 2, 3].map((s) => (
          <button
            key={s}
            type="button"
            className={`rounded-full px-3 py-1 ${step === s ? "bg-amber-600 text-zinc-950" : "bg-zinc-200 dark:bg-zinc-800"}`}
            onClick={() => setStep(s as 1 | 2 | 3)}
          >
            {s === 1 ? "1. Data" : s === 2 ? "2. Párování" : "3. Sklad"}
          </button>
        ))}
      </div>

      {step === 1 && (
        <div className="grid gap-3 rounded-lg border border-zinc-200 p-4 dark:border-zinc-800 md:grid-cols-2">
          <div><span className="text-xs text-zinc-500">Dodavatel</span><p>{inv.supplierName}</p></div>
          <div><span className="text-xs text-zinc-500">IČO / DIČ</span><p>{inv.supplierCompanyId ?? "—"} / {inv.supplierVatId ?? "—"}</p></div>
          <div><span className="text-xs text-zinc-500">Vystaveno</span><p>{new Date(inv.issueDate).toLocaleDateString("cs-CZ")}</p></div>
          <div><span className="text-xs text-zinc-500">Celkem</span><p>{inv.totalAmount.toLocaleString("cs-CZ")} Kč</p></div>
          {inv.notes && <div className="md:col-span-2"><span className="text-xs text-zinc-500">Poznámka</span><p>{inv.notes}</p></div>}
        </div>
      )}

      {(step === 1 || step === 2) && (
        <div className={tableWrap}>
          <table className="w-full text-left text-sm">
            <thead className={tableHead}>
              <tr>
                <th className="px-3 py-2">Popis</th>
                <th className="px-3 py-2">Množství</th>
                <th className="px-3 py-2">Kg</th>
                <th className="px-3 py-2">Kč/kg</th>
                {step === 2 && <th className="px-3 py-2">Filament</th>}
                {step === 2 && <th className="px-3 py-2">Shoda</th>}
                {step === 2 && <th className="px-3 py-2" />}
              </tr>
            </thead>
            <tbody className={tableBody}>
              {inv.lines.map((l) => (
                <tr key={l.id}>
                  <td className="px-3 py-2">{l.description}</td>
                  <td className="px-3 py-2">{l.quantity} {l.unit}</td>
                  <td className="px-3 py-2">{l.weightKg.toFixed(3)}</td>
                  <td className="px-3 py-2">{l.pricePerKg.toFixed(2)}</td>
                  {step === 2 && (
                    <>
                      <td className="px-3 py-2">
                        {readOnly ? (
                          l.filamentTypeName ?? "—"
                        ) : (
                          <select
                            className={inputClass}
                            value={l.filamentTypeId ?? ""}
                            onChange={(e) => {
                              const v = Number(e.target.value);
                              if (v) void manualMatch(l.id, v);
                            }}
                          >
                            <option value="">— vyberte —</option>
                            {types.map((t) => (
                              <option key={t.id} value={t.id}>{t.name}</option>
                            ))}
                          </select>
                        )}
                      </td>
                      <td className="px-3 py-2">
                        <span className={`rounded px-1.5 py-0.5 text-xs ${
                          l.matchStatus === "AutoMatched" ? "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300"
                          : l.matchStatus === "Suggested" ? "bg-amber-100 text-amber-900 dark:bg-amber-900/40 dark:text-amber-200"
                          : l.matchStatus === "ManualMatched" ? "bg-sky-100 text-sky-900 dark:bg-sky-900/40 dark:text-sky-200"
                          : "bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
                        }`}>
                          {MATCH[l.matchStatus] ?? l.matchStatus} {l.matchConfidence > 0 ? `${l.matchConfidence}%` : ""}
                        </span>
                      </td>
                      <td className="px-3 py-2">
                        {!readOnly && (
                          <button type="button" className="text-xs text-sky-600 hover:underline dark:text-sky-400" onClick={() => void createType(l.id)}>
                            Nový typ
                          </button>
                        )}
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {step === 2 && !readOnly && (
        <div className="flex gap-2">
          <button type="button" className={btnSecondary} disabled={busy} onClick={() => void runMatch()}>
            Spustit auto-párování
          </button>
          <button type="button" className={btnPrimary} disabled={busy || !allMatched} onClick={() => setStep(3)}>
            Pokračovat na sklad
          </button>
        </div>
      )}

      {step === 3 && (
        <div className="space-y-3 rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
          <h2 className="font-medium">Souhrn příjmu na sklad</h2>
          <ul className="space-y-1 text-sm">
            {inv.lines.map((l) => (
              <li key={l.id}>
                {l.filamentTypeName ?? "?"} — {l.weightKg.toFixed(3)} kg, {l.pricePerKg.toFixed(2)} Kč/kg ({l.pieceCount} ks)
              </li>
            ))}
          </ul>
          {!readOnly && (
            <button type="button" className={btnPrimary} disabled={busy || !allMatched} onClick={() => void postToStock()}>
              Zaúčtovat na sklad
            </button>
          )}
          {readOnly && <p className="text-sm text-green-700 dark:text-green-400">Faktura je již zaúčtovaná na sklad.</p>}
        </div>
      )}
    </div>
  );
}
