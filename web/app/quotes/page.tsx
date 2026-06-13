"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { CustomerSelect } from "@/components/CustomerSelect";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiUrl, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, inputClass, labelClass, linkAction, linkDanger, linkMuted, tableBody, tableHead, tableWrap } from "@/lib/ui";
import type { Lookups } from "@/lib/types";

type QuoteRow = {
  id: number;
  number: string;
  title: string;
  customerId: number;
  customerName: string;
  status: string;
  totalAmount: number;
  issueDate: string;
};

type CreateMode = "blank" | "calculations";

export default function QuotesPage() {
  const router = useRouter();
  const [items, setItems] = useState<QuoteRow[]>([]);
  const [lookups, setLookups] = useState<Lookups | null>(null);
  const [calcs, setCalcs] = useState<{ id: number; title: string; customerId: number | null }[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [createMode, setCreateMode] = useState<CreateMode>("blank");
  const [blankCustomer, setBlankCustomer] = useState<number | "">("");
  const [selCalc, setSelCalc] = useState<number[]>([]);
  const [detailed, setDetailed] = useState(true);
  const [filterCustomer, setFilterCustomer] = useState<number | "">("");
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      const [q, l, c] = await Promise.all([
        apiJson<QuoteRow[]>("/api/quotes"),
        apiJson<Lookups>("/api/lookups"),
        apiJson<{ id: number; title: string; customerId: number | null }[]>("/api/calculations"),
      ]);
      setItems(q);
      setLookups(l);
      setCalcs(c);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const filteredCalcs = calcs.filter((c) => !filterCustomer || c.customerId === filterCustomer);

  function openCreate() {
    setCreateMode("blank");
    setBlankCustomer("");
    setSelCalc([]);
    setMsg(null);
    setCreateOpen(true);
  }

  async function submitCreate() {
    setMsg(null);
    try {
      if (createMode === "blank") {
        if (blankCustomer === "") {
          setMsg("Vyberte zákazníka.");
          return;
        }
        const res = await apiJson<{ id: number }>("/api/quotes", {
          method: "POST",
          body: JSON.stringify({ customerId: blankCustomer, title: null, lines: null }),
        });
        router.push(`/quotes/${res.id}`);
      } else {
        if (selCalc.length === 0) {
          setMsg("Vyberte kalkulace.");
          return;
        }
        const res = await apiJson<{ id: number }>("/api/quotes/from-calculations", {
          method: "POST",
          body: JSON.stringify({
            calculationIds: selCalc,
            detailed,
            customerId: filterCustomer === "" ? null : filterCustomer,
          }),
        });
        router.push(`/quotes/${res.id}`);
      }
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function del(id: number) {
    if (!confirm("Smazat nabídku?")) return;
    try {
      await apiVoid(`/api/quotes/${id}`, { method: "DELETE" });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  if (!lookups) return <p className="text-zinc-500">Načítám…</p>;

  return (
    <div className="space-y-4">
      <PageHeader title="Nabídky" description="Seznam nabídek — řádky upravíte v detailu.">
        <button type="button" onClick={openCreate} className={btnPrimary}>
          + Nová nabídka
        </button>
      </PageHeader>

      {msg && !createOpen && <StatusBanner message={msg} />}

      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Číslo</th>
              <th className="px-3 py-2">Název</th>
              <th className="px-3 py-2">Zákazník</th>
              <th className="px-3 py-2">Částka</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-zinc-500">
                  Žádné nabídky.{" "}
                  <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={openCreate}>
                    Vytvořit první
                  </button>
                </td>
              </tr>
            ) : (
              items.map((q) => (
                <tr key={q.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                  <td className="px-3 py-2 font-mono text-zinc-500">{q.number}</td>
                  <td className="px-3 py-2 text-zinc-800 dark:text-zinc-200">{q.title}</td>
                  <td className="px-3 py-2 text-zinc-500">{q.customerName}</td>
                  <td className="px-3 py-2">{q.totalAmount} Kč</td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    <Link href={`/quotes/${q.id}`} className={linkAction}>Upravit</Link>
                    <a href={apiUrl(`/api/quotes/${q.id}/pdf`)} target="_blank" rel="noreferrer" className={linkMuted}>PDF</a>
                    <button type="button" className={linkDanger} onClick={() => void del(q.id)}>Smazat</button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="Nová nabídka"
        size="lg"
        footer={
          <>
            <button type="button" onClick={() => setCreateOpen(false)} className={btnSecondary}>Zrušit</button>
            <button type="button" onClick={() => void submitCreate()} className={btnPrimary}>Vytvořit a upravit</button>
          </>
        }
      >
        {msg && createOpen && <StatusBanner message={msg} />}
        <div className="mb-4 flex flex-wrap gap-2">
          {([
            ["blank", "Prázdná"],
            ["calculations", "Z kalkulací"],
          ] as const).map(([id, label]) => (
            <button
              key={id}
              type="button"
              onClick={() => { setCreateMode(id); setMsg(null); }}
              className={`rounded-md px-3 py-1.5 text-sm ${createMode === id ? "bg-amber-600 text-zinc-900 dark:text-zinc-950" : btnSecondary}`}
            >
              {label}
            </button>
          ))}
        </div>
        {createMode === "blank" ? (
          <label className={labelClass}>
            Zákazník *
            <div className="mt-1">
              <CustomerSelect customers={lookups.customers} value={blankCustomer} onChange={setBlankCustomer} />
            </div>
          </label>
        ) : (
          <div className="space-y-3">
            <div className="flex flex-wrap items-end gap-3">
              <label className={labelClass}>
                Filtrovat zákazníka
                <select className={inputClass} value={filterCustomer} onChange={(e) => setFilterCustomer(e.target.value === "" ? "" : parseInt(e.target.value, 10))}>
                  <option value="">Všichni</option>
                  {lookups.customers.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </label>
              <label className="flex items-center gap-2 pb-1 text-sm text-zinc-600 dark:text-zinc-300">
                <input type="checkbox" checked={detailed} onChange={(e) => setDetailed(e.target.checked)} />
                Detailní řádky
              </label>
            </div>
            <div className="max-h-48 overflow-y-auto rounded border border-zinc-200 dark:border-zinc-800">
              {filteredCalcs.map((c) => (
                <label key={c.id} className="flex cursor-pointer items-center gap-2 border-b border-zinc-200 px-2 py-1.5 text-sm hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800/50">
                  <input
                    type="checkbox"
                    checked={selCalc.includes(c.id)}
                    onChange={(e) => {
                      if (e.target.checked) setSelCalc((s) => [...s, c.id]);
                      else setSelCalc((s) => s.filter((x) => x !== c.id));
                    }}
                  />
                  <span>{c.title}</span>
                  <span className="text-xs text-zinc-500">#{c.id}</span>
                </label>
              ))}
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
