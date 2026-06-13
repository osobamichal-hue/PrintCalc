"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { CustomerSelect } from "@/components/CustomerSelect";
import { apiJson, apiVoid } from "@/lib/api";
import type { Lookups } from "@/lib/types";

type Line = {
  id: number;
  description: string;
  quantity: number;
  unitPrice: number;
  taxRatePercent: number;
  lineTotal: number;
  sourceCalculationId: number | null;
  sourceOrderId: number | null;
  sourceOrderLineId: number | null;
};

type Detail = {
  id: number;
  number: string;
  customerId: number;
  customerName: string;
  status: string;
  totalAmount: number;
  paidAmount: number;
  issueDate: string;
  dueDate: string | null;
  paymentMethod: string | null;
  notes: string | null;
  lines: Line[];
};

const statuses = ["Draft", "Issued", "Paid", "Overdue", "Cancelled"] as const;
const STATUS_LABELS: Record<string, string> = {
  Draft: "Koncept",
  Issued: "Vystaveno",
  Paid: "Uhrazeno",
  Overdue: "Po splatnosti",
  Cancelled: "Zrušeno",
};

export default function InvoiceDetailPage() {
  const params = useParams();
  const id = parseInt(String(params.id), 10);
  const [data, setData] = useState<Detail | null>(null);
  const [customers, setCustomers] = useState<Lookups["customers"]>([]);
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (Number.isNaN(id)) return;
    setMsg(null);
    try {
      const [detail, lookups] = await Promise.all([
        apiJson<Detail>(`/api/invoices/${id}`),
        apiJson<Lookups>("/api/lookups"),
      ]);
      setData(detail);
      setCustomers(lookups.customers);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function markPaid() {
    if (!data) return;
    setMsg(null);
    try {
      await apiVoid(`/api/invoices/${id}/mark-paid`, {
        method: "PATCH",
        body: JSON.stringify({ paidAmount: data.totalAmount }),
      });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function save() {
    if (!data) return;
    try {
      await apiVoid(`/api/invoices/${id}`, {
        method: "PUT",
        body: JSON.stringify({
          customerId: data.customerId,
          status: data.status,
          paymentMethod: data.paymentMethod,
          notes: data.notes,
          issueDate: data.issueDate,
          dueDate: data.dueDate,
          paidAmount: data.paidAmount,
          lines: data.lines.map((l) => ({
            sourceCalculationId: l.sourceCalculationId,
            sourceOrderId: l.sourceOrderId,
            sourceOrderLineId: l.sourceOrderLineId,
            description: l.description,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            taxRatePercent: l.taxRatePercent,
          })),
        }),
      });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  function addLine() {
    if (!data) return;
    setData({
      ...data,
      lines: [
        ...data.lines,
        {
          id: -Date.now(),
          description: "Položka",
          quantity: 1,
          unitPrice: 0,
          taxRatePercent: 21,
          lineTotal: 0,
          sourceCalculationId: null,
          sourceOrderId: null,
          sourceOrderLineId: null,
        },
      ],
    });
  }

  function updateLine(i: number, patch: Partial<Line>) {
    if (!data) return;
    const lines = [...data.lines];
    lines[i] = { ...lines[i], ...patch };
    const l = lines[i];
    l.lineTotal = Math.round(l.quantity * l.unitPrice);
    setData({ ...data, lines });
  }

  function removeLine(i: number) {
    if (!data) return;
    setData({ ...data, lines: data.lines.filter((_, j) => j !== i) });
  }

  if (!data) return <p className="text-zinc-500">{msg ?? "Načítám…"}</p>;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Faktura {data.number}</h1>
        <div className="flex flex-wrap items-center gap-2">
          {data.status !== "Paid" && data.status !== "Cancelled" && (
            <button
              type="button"
              onClick={() => void markPaid()}
              className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
            >
              FA uhrazena
            </button>
          )}
          <Link href="/invoices" className="text-sm text-sky-400 hover:underline">
            ← Seznam
          </Link>
        </div>
      </div>
      {msg && (
        <div className="rounded border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          {msg}
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-xs text-zinc-500">
          Odběratel
          <div className="mt-1">
            <CustomerSelect
              customers={customers}
              value={data.customerId}
              onChange={(v) =>
                setData({
                  ...data,
                  customerId: v === "" ? data.customerId : v,
                })
              }
            />
          </div>
        </label>
        <label className="text-xs text-zinc-500">
          Stav
          <select
            className="mt-1 w-full rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1.5 text-sm"
            value={data.status}
            onChange={(e) => setData({ ...data, status: e.target.value })}
          >
            {statuses.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s] ?? s}
              </option>
            ))}
          </select>
        </label>
        <label className="text-xs text-zinc-500">
          Uhrazeno (Kč)
          <input
            type="number"
            className="mt-1 w-full rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1.5 text-sm"
            value={data.paidAmount}
            onChange={(e) =>
              setData({
                ...data,
                paidAmount: parseFloat(e.target.value) || 0,
              })
            }
          />
        </label>
        <label className="text-xs text-zinc-500">
          Platba
          <input
            className="mt-1 w-full rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1.5 text-sm"
            value={data.paymentMethod ?? ""}
            onChange={(e) =>
              setData({ ...data, paymentMethod: e.target.value || null })
            }
          />
        </label>
      </div>
      <label className="text-xs text-zinc-500">
        Poznámka
        <textarea
          className="mt-1 w-full rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1.5 text-sm"
          rows={2}
          value={data.notes ?? ""}
          onChange={(e) => setData({ ...data, notes: e.target.value || null })}
        />
      </label>
      <div className="space-y-2">
        <div className="flex justify-between">
          <h2 className="text-sm font-medium text-zinc-500 dark:text-zinc-400">Řádky (bez DPH)</h2>
          <button type="button" onClick={addLine} className="text-xs text-sky-400">
            + řádek
          </button>
        </div>
        {data.lines.map((l, i) => (
          <div
            key={l.id}
            className="grid gap-2 rounded border border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900/40 p-2 sm:grid-cols-12"
          >
            <input
              className="sm:col-span-4 rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm"
              value={l.description}
              onChange={(e) => updateLine(i, { description: e.target.value })}
            />
            <input
              type="number"
              className="sm:col-span-2 rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm"
              value={l.quantity}
              onChange={(e) =>
                updateLine(i, { quantity: parseFloat(e.target.value) || 0 })
              }
            />
            <input
              type="number"
              className="sm:col-span-2 rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm"
              value={l.unitPrice}
              onChange={(e) =>
                updateLine(i, { unitPrice: parseFloat(e.target.value) || 0 })
              }
            />
            <input
              type="number"
              title="DPH %"
              className="sm:col-span-2 rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm"
              value={l.taxRatePercent}
              onChange={(e) =>
                updateLine(i, {
                  taxRatePercent: parseFloat(e.target.value) || 0,
                })
              }
            />
            <span className="sm:col-span-1 self-center text-sm text-zinc-500 dark:text-zinc-400">
              {l.lineTotal}
            </span>
            <button
              type="button"
              className="text-xs text-red-400"
              onClick={() => removeLine(i)}
            >
              ×
            </button>
          </div>
        ))}
      </div>
      <button
        type="button"
        onClick={() => void save()}
        className="rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-zinc-900 dark:text-zinc-950"
      >
        Uložit
      </button>
    </div>
  );
}
