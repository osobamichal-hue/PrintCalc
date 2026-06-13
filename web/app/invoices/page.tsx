"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { CustomerSelect } from "@/components/CustomerSelect";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiUrl, apiVoid } from "@/lib/api";

const INV_STATUS: Record<string, string> = {
  Draft: "Koncept",
  Issued: "Vystaveno",
  Paid: "Uhrazeno",
  Overdue: "Po splatnosti",
  Cancelled: "Zrušeno",
};
import { btnPrimary, btnSecondary, inputClass, labelClass, linkAction, linkDanger, linkMuted, tableBody, tableHead, tableWrap } from "@/lib/ui";
import type { Lookups } from "@/lib/types";

type InvRow = {
  id: number;
  number: string;
  customerId: number;
  customerName: string;
  status: string;
  totalAmount: number;
  issueDate: string;
};

type SourceTab = "blank" | "orders" | "quotes" | "calculations";

export default function InvoicesPage() {
  const router = useRouter();
  const [items, setItems] = useState<InvRow[]>([]);
  const [orders, setOrders] = useState<{ id: number; number: string; title: string }[]>([]);
  const [quotes, setQuotes] = useState<{ id: number; number: string; title: string }[]>([]);
  const [calcs, setCalcs] = useState<
    { id: number; title: string; customerId: number | null; totalWithMargin: number }[]
  >([]);
  const [customers, setCustomers] = useState<Lookups["customers"]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [tab, setTab] = useState<SourceTab>("blank");
  const [blankCustomer, setBlankCustomer] = useState<number | "">("");
  const [sel, setSel] = useState<number[]>([]);
  const [detailed, setDetailed] = useState(true);
  const [dueDays, setDueDays] = useState(14);
  const [payment, setPayment] = useState("Převodem");
  const [prefix, setPrefix] = useState("");
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      const [i, o, q, c, lookups] = await Promise.all([
        apiJson<InvRow[]>("/api/invoices"),
        apiJson<{ id: number; number: string; title: string }[]>("/api/orders"),
        apiJson<{ id: number; number: string; title: string }[]>("/api/quotes"),
        apiJson<
          { id: number; title: string; customerId: number | null; totalWithMargin: number }[]
        >("/api/calculations"),
        apiJson<Lookups>("/api/lookups"),
      ]);
      setItems(i);
      setOrders(o);
      setQuotes(q);
      setCalcs(c);
      setCustomers(lookups.customers);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function openCreate() {
    setTab("blank");
    setBlankCustomer("");
    setSel([]);
    setMsg(null);
    setCreateOpen(true);
  }

  function invoiceBody(idsKey: string, ids: number[]) {
    return {
      [idsKey]: ids,
      detailed,
      dueDays,
      paymentMethod: payment,
      invoiceNumberPrefix: prefix || null,
    };
  }

  async function submitCreate() {
    setMsg(null);
    try {
      if (tab === "blank") {
        if (blankCustomer === "") {
          setMsg("Vyberte odběratele.");
          return;
        }
        const res = await apiJson<{ id: number }>("/api/invoices", {
          method: "POST",
          body: JSON.stringify({
            customerId: blankCustomer,
            dueDays,
            paymentMethod: payment,
            invoiceNumberPrefix: prefix || null,
            lines: null,
          }),
        });
        router.push(`/invoices/${res.id}`);
      } else {
        if (sel.length === 0) {
          setMsg("Vyberte položky ze seznamu.");
          return;
        }
        const endpoints: Record<Exclude<SourceTab, "blank">, { path: string; key: string }> = {
          orders: { path: "/api/invoices/from-orders", key: "orderIds" },
          quotes: { path: "/api/invoices/from-quotes", key: "quoteIds" },
          calculations: { path: "/api/invoices/from-calculations", key: "calculationIds" },
        };
        const { path, key } = endpoints[tab];
        const res = await apiJson<{ id: number }>(path, {
          method: "POST",
          body: JSON.stringify(invoiceBody(key, sel)),
        });
        router.push(`/invoices/${res.id}`);
      }
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function markPaid(id: number, totalAmount: number) {
    try {
      await apiVoid(`/api/invoices/${id}/mark-paid`, {
        method: "PATCH",
        body: JSON.stringify({ paidAmount: totalAmount }),
      });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function del(id: number) {
    if (!confirm("Smazat fakturu?")) return;
    try {
      await apiVoid(`/api/invoices/${id}`, { method: "DELETE" });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  const tabs: { id: SourceTab; label: string }[] = [
    { id: "blank", label: "Prázdná" },
    { id: "orders", label: "Ze zakázek" },
    { id: "quotes", label: "Z nabídek" },
    { id: "calculations", label: "Z kalkulací" },
  ];

  const sourceList =
    tab === "orders"
      ? orders.map((x) => ({ id: x.id, left: x.number, right: x.title }))
      : tab === "quotes"
        ? quotes.map((x) => ({ id: x.id, left: x.number, right: x.title }))
        : tab === "calculations"
          ? calcs.map((x) => ({
              id: x.id,
              left: `#${x.id}`,
              right: `${x.title} (${x.totalWithMargin} Kč)`,
            }))
          : [];

  return (
    <div className="space-y-4">
      <PageHeader title="Faktury" description="Seznam faktur — vystavit lze přímo nebo z jiného dokladu.">
        <button type="button" onClick={openCreate} className={btnPrimary}>
          + Nová faktura
        </button>
      </PageHeader>

      {msg && !createOpen && <StatusBanner message={msg} />}

      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Číslo</th>
              <th className="px-3 py-2">Zákazník</th>
              <th className="px-3 py-2">Částka</th>
              <th className="px-3 py-2">Stav</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-zinc-500">
                  Žádné faktury.{" "}
                  <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={openCreate}>
                    Vystavit první
                  </button>
                </td>
              </tr>
            ) : (
              items.map((inv) => (
                <tr key={inv.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                  <td className="px-3 py-2 font-mono text-zinc-500">{inv.number}</td>
                  <td className="px-3 py-2 text-zinc-800 dark:text-zinc-200">{inv.customerName}</td>
                  <td className="px-3 py-2">{inv.totalAmount} Kč</td>
                  <td className="px-3 py-2 text-zinc-500">{INV_STATUS[inv.status] ?? inv.status}</td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    {inv.status !== "Paid" && inv.status !== "Cancelled" && (
                      <button
                        type="button"
                        className="text-xs font-medium text-emerald-600 hover:underline dark:text-emerald-400"
                        onClick={() => void markPaid(inv.id, inv.totalAmount)}
                      >
                        Uhrazena
                      </button>
                    )}
                    <Link href={`/invoices/${inv.id}`} className={linkAction}>Upravit</Link>
                    <a href={apiUrl(`/api/invoices/${inv.id}/pdf`)} target="_blank" rel="noreferrer" className={linkMuted}>PDF</a>
                    <a href={apiUrl(`/api/invoices/${inv.id}/csv`)} download className={linkMuted}>CSV</a>
                    <button type="button" className={linkDanger} onClick={() => void del(inv.id)}>Smazat</button>
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
        title="Nová faktura"
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
          {tabs.map((t) => (
            <button
              key={t.id}
              type="button"
              onClick={() => { setTab(t.id); setSel([]); setMsg(null); }}
              className={`rounded-md px-3 py-1.5 text-sm ${tab === t.id ? "bg-amber-600 text-zinc-900 dark:text-zinc-950" : btnSecondary}`}
            >
              {t.label}
            </button>
          ))}
        </div>

        <div className="mb-4 flex flex-wrap gap-3">
          <label className={labelClass}>
            Splatnost (dny)
            <input type="number" className={`${inputClass} w-24`} value={dueDays} onChange={(e) => setDueDays(parseInt(e.target.value, 10) || 14)} />
          </label>
          <label className={labelClass}>
            Platba
            <input className={inputClass} value={payment} onChange={(e) => setPayment(e.target.value)} />
          </label>
          <label className={labelClass}>
            Prefix čísla
            <input className={`${inputClass} w-28`} value={prefix} onChange={(e) => setPrefix(e.target.value)} placeholder="volitelné" />
          </label>
          {tab !== "blank" && (
            <label className="flex items-end gap-2 pb-1 text-sm text-zinc-600 dark:text-zinc-300">
              <input type="checkbox" checked={detailed} onChange={(e) => setDetailed(e.target.checked)} />
              Detailní řádky
            </label>
          )}
        </div>

        {tab === "blank" ? (
          <label className={labelClass}>
            Odběratel *
            <div className="mt-1">
              <CustomerSelect customers={customers} value={blankCustomer} onChange={setBlankCustomer} />
            </div>
          </label>
        ) : (
          <div className="max-h-48 overflow-y-auto rounded border border-zinc-200 dark:border-zinc-800">
            {sourceList.map((row) => (
              <label key={row.id} className="flex cursor-pointer items-center gap-2 border-b border-zinc-200 px-2 py-1.5 text-sm hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800/50">
                <input
                  type="checkbox"
                  checked={sel.includes(row.id)}
                  onChange={(e) => {
                    if (e.target.checked) setSel((s) => [...s, row.id]);
                    else setSel((s) => s.filter((x) => x !== row.id));
                  }}
                />
                <span className="font-mono text-zinc-500">{row.left}</span>
                <span>{row.right}</span>
              </label>
            ))}
            {sourceList.length === 0 && (
              <p className="px-2 py-3 text-sm text-zinc-500">Žádné položky k výběru.</p>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
}
