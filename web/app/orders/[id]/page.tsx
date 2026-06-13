"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { CustomerSelect } from "@/components/CustomerSelect";
import { apiJson, apiVoid } from "@/lib/api";
import type { Lookups } from "@/lib/types";

type Line = {
  id: number;
  sourceCalculationId: number | null;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
};

type Detail = {
  id: number;
  number: string;
  title: string;
  customerId: number;
  customerName: string;
  status: string;
  totalAmount: number;
  lines: Line[];
};

const statuses = [
  "Draft",
  "Confirmed",
  "InProduction",
  "Completed",
  "Cancelled",
] as const;

export default function OrderDetailPage() {
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
        apiJson<Detail>(`/api/orders/${id}`),
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

  async function save() {
    if (!data) return;
    try {
      await apiVoid(`/api/orders/${id}`, {
        method: "PUT",
        body: JSON.stringify({
          customerId: data.customerId,
          title: data.title,
          status: data.status,
          lines: data.lines.map((l) => ({
            sourceCalculationId: l.sourceCalculationId,
            description: l.description,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
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
          sourceCalculationId: null,
          description: "Položka",
          quantity: 1,
          unitPrice: 0,
          lineTotal: 0,
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
      <div className="flex justify-between gap-4">
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Zakázka {data.number}</h1>
        <Link href="/orders" className="text-sm text-sky-400 hover:underline">
          ← Seznam
        </Link>
      </div>
      {msg && (
        <div className="rounded border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          {msg}
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-xs text-zinc-500">
          Zákazník
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
          Název
          <input
            className="mt-1 w-full rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1.5 text-sm"
            value={data.title}
            onChange={(e) => setData({ ...data, title: e.target.value })}
          />
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
                {s}
              </option>
            ))}
          </select>
        </label>
      </div>
      <p className="text-sm text-zinc-500">Zákazník: {data.customerName}</p>
      <div className="space-y-2">
        <div className="flex justify-between">
          <h2 className="text-sm font-medium text-zinc-500 dark:text-zinc-400">Řádky</h2>
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
              className="sm:col-span-5 rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm"
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
            <span className="sm:col-span-2 self-center text-sm text-zinc-500 dark:text-zinc-400">
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
