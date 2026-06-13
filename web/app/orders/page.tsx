"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { CustomerSelect } from "@/components/CustomerSelect";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, linkAction, linkDanger, tableBody, tableHead, tableWrap } from "@/lib/ui";
import type { Lookups } from "@/lib/types";

type OrderRow = {
  id: number;
  number: string;
  title: string;
  customerId: number;
  customerName: string;
  status: string;
  totalAmount: number;
  createdAt: string;
};

type QuotePick = { id: number; number: string; title: string; customerId: number };
type CreateMode = "blank" | "quotes";

export default function OrdersPage() {
  const router = useRouter();
  const [items, setItems] = useState<OrderRow[]>([]);
  const [quotes, setQuotes] = useState<QuotePick[]>([]);
  const [customers, setCustomers] = useState<Lookups["customers"]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [createMode, setCreateMode] = useState<CreateMode>("blank");
  const [blankCustomer, setBlankCustomer] = useState<number | "">("");
  const [sel, setSel] = useState<number[]>([]);
  const [detailed, setDetailed] = useState(true);
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      const [o, q, lookups] = await Promise.all([
        apiJson<OrderRow[]>("/api/orders"),
        apiJson<{ id: number; number: string; title: string; customerId: number }[]>("/api/quotes"),
        apiJson<Lookups>("/api/lookups"),
      ]);
      setItems(o);
      setQuotes(q);
      setCustomers(lookups.customers);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function openCreate() {
    setCreateMode("blank");
    setBlankCustomer("");
    setSel([]);
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
        const res = await apiJson<{ id: number }>("/api/orders", {
          method: "POST",
          body: JSON.stringify({ customerId: blankCustomer, title: null, lines: null }),
        });
        router.push(`/orders/${res.id}`);
      } else {
        if (sel.length === 0) {
          setMsg("Vyberte nabídky.");
          return;
        }
        const res = await apiJson<{ id: number }>("/api/orders/from-quotes", {
          method: "POST",
          body: JSON.stringify({ quoteIds: sel, detailed }),
        });
        router.push(`/orders/${res.id}`);
      }
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function del(id: number) {
    if (!confirm("Smazat zakázku?")) return;
    try {
      await apiVoid(`/api/orders/${id}`, { method: "DELETE" });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Zakázky" description="Seznam zakázek — zakázku lze přeskočit a jít rovnou na fakturu.">
        <button type="button" onClick={openCreate} className={btnPrimary}>
          + Nová zakázka
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
                  Žádné zakázky.{" "}
                  <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={openCreate}>
                    Vytvořit první
                  </button>
                </td>
              </tr>
            ) : (
              items.map((o) => (
                <tr key={o.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                  <td className="px-3 py-2 font-mono text-zinc-500">{o.number}</td>
                  <td className="px-3 py-2 text-zinc-800 dark:text-zinc-200">{o.title}</td>
                  <td className="px-3 py-2 text-zinc-500">{o.customerName}</td>
                  <td className="px-3 py-2">{o.totalAmount} Kč</td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    <Link href={`/orders/${o.id}`} className={linkAction}>Upravit</Link>
                    <button type="button" className={linkDanger} onClick={() => void del(o.id)}>Smazat</button>
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
        title="Nová zakázka"
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
            ["quotes", "Z nabídek"],
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
          <label className="block text-xs text-zinc-500">
            Zákazník *
            <div className="mt-1">
              <CustomerSelect customers={customers} value={blankCustomer} onChange={setBlankCustomer} />
            </div>
          </label>
        ) : (
          <div className="space-y-3">
            <label className="flex items-center gap-2 text-sm text-zinc-600 dark:text-zinc-300">
              <input type="checkbox" checked={detailed} onChange={(e) => setDetailed(e.target.checked)} />
              Detailní řádky
            </label>
            <div className="max-h-48 overflow-y-auto rounded border border-zinc-200 dark:border-zinc-800">
              {quotes.map((q) => (
                <label key={q.id} className="flex cursor-pointer items-center gap-2 border-b border-zinc-200 px-2 py-1.5 text-sm hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800/50">
                  <input
                    type="checkbox"
                    checked={sel.includes(q.id)}
                    onChange={(e) => {
                      if (e.target.checked) setSel((s) => [...s, q.id]);
                      else setSel((s) => s.filter((x) => x !== q.id));
                    }}
                  />
                  <span className="font-mono text-zinc-500">{q.number}</span>
                  <span>{q.title}</span>
                </label>
              ))}
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
