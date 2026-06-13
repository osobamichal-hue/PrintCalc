"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, linkAction, linkDanger, tableBody, tableHead, tableWrap } from "@/lib/ui";

const STATUS: Record<string, string> = {
  Draft: "Koncept",
  ReadyToMatch: "K párování",
  Matched: "Spárováno",
  Posted: "Na skladě",
  Cancelled: "Zrušeno",
};

const SOURCE: Record<string, string> = {
  Manual: "Ručně",
  Isdoc: "ISDOC",
  Xml: "XML",
  Csv: "CSV",
  Excel: "Excel",
  Pdf: "PDF",
};

type Row = {
  id: number;
  number: string;
  supplierName: string;
  issueDate: string;
  status: string;
  importSource: string;
  totalAmount: number;
  lineCount: number;
  matchedLineCount: number;
};

export default function PurchaseInvoicesPage() {
  const [items, setItems] = useState<Row[]>([]);
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      setItems(await apiJson<Row[]>("/api/purchase-invoices"));
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function remove(id: number) {
    if (!confirm("Smazat přijatou fakturu?")) return;
    setMsg(null);
    try {
      await apiVoid(`/api/purchase-invoices/${id}`, { method: "DELETE" });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Přijaté faktury"
        description="Import, párování na filamenty a automatický příjem na sklad."
      >
        <Link href="/purchase-invoices/import" className={btnSecondary}>
          Import souboru
        </Link>
        <Link href="/purchase-invoices/new" className={btnPrimary}>
          Nová FA
        </Link>
      </PageHeader>
      {msg && <StatusBanner message={msg} />}
      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Číslo</th>
              <th className="px-3 py-2">Dodavatel</th>
              <th className="px-3 py-2">Datum</th>
              <th className="px-3 py-2">Stav</th>
              <th className="px-3 py-2">Zdroj</th>
              <th className="px-3 py-2 text-right">Celkem</th>
              <th className="px-3 py-2">Párování</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {items.map((x) => (
              <tr key={x.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/40">
                <td className="px-3 py-2 font-medium">{x.number}</td>
                <td className="px-3 py-2">{x.supplierName}</td>
                <td className="px-3 py-2">{new Date(x.issueDate).toLocaleDateString("cs-CZ")}</td>
                <td className="px-3 py-2">{STATUS[x.status] ?? x.status}</td>
                <td className="px-3 py-2">{SOURCE[x.importSource] ?? x.importSource}</td>
                <td className="px-3 py-2 text-right">{x.totalAmount.toLocaleString("cs-CZ")} Kč</td>
                <td className="px-3 py-2">
                  {x.lineCount > 0
                    ? `${Math.round((100 * x.matchedLineCount) / x.lineCount)} % (${x.matchedLineCount}/${x.lineCount})`
                    : "—"}
                </td>
                <td className="px-3 py-2 text-right whitespace-nowrap">
                  <Link href={`/purchase-invoices/${x.id}`} className={linkAction}>
                    Detail
                  </Link>
                  {x.status !== "Posted" && (
                    <>
                      {" · "}
                      <button type="button" className={linkDanger} onClick={() => void remove(x.id)}>
                        Smazat
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={8} className="px-3 py-8 text-center text-zinc-500">
                  Zatím žádné přijaté faktury.{" "}
                  <Link href="/purchase-invoices/import" className="text-sky-600 hover:underline">
                    Importujte první FA
                  </Link>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
