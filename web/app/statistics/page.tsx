"use client";

import { useCallback, useEffect, useState } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import Link from "next/link";
import { apiJson, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, linkAction, tableBody, tableHead, tableWrap } from "@/lib/ui";

type Dashboard = {
  periodMonths: number;
  overview: {
    quotesCount: number;
    quotesValue: number;
    ordersCount: number;
    ordersValue: number;
    invoicesCount: number;
    invoicedValue: number;
    paidValue: number;
    calculationsCount: number;
    calculationsCost: number;
    calculationsRevenue: number;
    calculationsProfit: number;
    averageMarginPercent: number;
    invoicedEstimatedProfit: number;
    invoicedEstimatedCost: number;
    totalPrintHours: number;
  };
  monthlyTrend: {
    label: string;
    quotes: number;
    orders: number;
    invoices: number;
    paid: number;
    costs: number;
    revenue: number;
    profit: number;
  }[];
  costBreakdown: {
    material: number;
    print: number;
    energy: number;
    modelDesign: number;
    startFee: number;
    margin: number;
  };
  pipeline: {
    quotesTotal: number;
    ordersTotal: number;
    invoicesTotal: number;
    paidTotal: number;
    quoteAcceptancePercent: number;
    orderCompletionPercent: number;
    invoicePaidPercent: number;
  };
  topCustomers: {
    customerId: number;
    customerName: string;
    invoicedValue: number;
    invoiceCount: number;
    quotedValue: number;
  }[];
  printerUtilization: {
    printerId: number;
    printerName: string;
    printHours: number;
    revenue: number;
    calculationCount: number;
  }[];
  quotesByStatus: { status: string; count: number }[];
  ordersByStatus: { status: string; count: number }[];
  invoicesByStatus: { status: string; count: number }[];
  quoteInvoiceGap: {
    customerId: number;
    customerName: string;
    quotedValue: number;
    invoicedValue: number;
    gap: number;
  }[];
  openInvoices: {
    id: number;
    number: string;
    customerName: string;
    totalAmount: number;
    paidAmount: number;
    status: string;
    dueDate: string | null;
    issueDate: string;
  }[];
  openInvoicesRemaining: number;
};

const PIE_COLORS = ["#f59e0b", "#3b82f6", "#10b981", "#8b5cf6", "#ef4444", "#06b6d4"];
const STATUS_LABELS: Record<string, string> = {
  Draft: "Koncept",
  Sent: "Odesláno",
  Accepted: "Přijato",
  Rejected: "Zamítnuto",
  Confirmed: "Potvrzeno",
  InProduction: "Ve výrobě",
  Completed: "Dokončeno",
  Cancelled: "Zrušeno",
  Issued: "Vystaveno",
  Paid: "Zaplaceno",
  Overdue: "Po splatnosti",
};

function fmt(n: number) {
  return new Intl.NumberFormat("cs-CZ", { maximumFractionDigits: 0 }).format(n);
}

function fmtKc(n: number) {
  return `${fmt(n)} Kč`;
}

function KpiCard({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-700 dark:bg-zinc-900/60">
      <p className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-zinc-900 dark:text-zinc-50">{value}</p>
      {sub && <p className="mt-1 text-xs text-zinc-500">{sub}</p>}
    </div>
  );
}

function ChartCard({ title, children, className }: { title: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={`rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-700 dark:bg-zinc-900/60 ${className ?? ""}`}>
      <h2 className="mb-4 text-sm font-medium text-zinc-700 dark:text-zinc-300">{title}</h2>
      {children}
    </section>
  );
}

export default function StatisticsPage() {
  const [data, setData] = useState<Dashboard | null>(null);
  const [months, setMonths] = useState(12);
  const [msg, setMsg] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setMsg(null);
    try {
      setData(await apiJson<Dashboard>(`/api/statistics/dashboard?months=${months}`));
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba načtení statistik");
    } finally {
      setLoading(false);
    }
  }, [months]);

  useEffect(() => {
    void load();
  }, [load]);

  async function markPaid(id: number, totalAmount: number) {
    setMsg(null);
    try {
      await apiVoid(`/api/invoices/${id}/mark-paid`, {
        method: "PATCH",
        body: JSON.stringify({ paidAmount: totalAmount }),
      });
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba při označení úhrady");
    }
  }

  if (loading && !data) {
    return <p className="text-zinc-500">Načítám statistiky…</p>;
  }

  if (!data) {
    return (
      <div className="space-y-4">
        <PageHeader title="Statistiky" description="Přehled zisku, nákladů a dokladů." />
        {msg && <StatusBanner message={msg} variant="error" />}
      </div>
    );
  }

  const { overview: o, costBreakdown: cb, pipeline: p } = data;

  const costPie = [
    { name: "Materiál", value: cb.material },
    { name: "Tisk (stroj)", value: cb.print },
    { name: "Energie", value: cb.energy },
    { name: "Modelování", value: cb.modelDesign },
    { name: "Start fee", value: cb.startFee },
    { name: "Marže (zisk)", value: cb.margin },
  ].filter((x) => x.value > 0);

  const pipelineBar = [
    { name: "Nabídky", value: p.quotesTotal },
    { name: "Zakázky", value: p.ordersTotal },
    { name: "Faktury", value: p.invoicesTotal },
    { name: "Zaplaceno", value: p.paidTotal },
  ];

  const profitVsCostMonthly = data.monthlyTrend.map((m) => ({
    label: m.label,
    Náklady: m.costs,
    Tržby: m.revenue,
    Zisk: m.profit,
  }));

  const documentsMonthly = data.monthlyTrend.map((m) => ({
    label: m.label,
    Nabídky: m.quotes,
    Zakázky: m.orders,
    Faktury: m.invoices,
  }));

  const topCustomersChart = data.topCustomers.map((c) => ({
    name: c.customerName.length > 18 ? `${c.customerName.slice(0, 16)}…` : c.customerName,
    Fakturováno: c.invoicedValue,
    Nabídnuto: c.quotedValue,
  }));

  const printerChart = data.printerUtilization.map((pr) => ({
    name: pr.printerName,
    Hodiny: Math.round(pr.printHours * 10) / 10,
    Tržby: pr.revenue,
  }));

  const gapChart = data.quoteInvoiceGap.map((g) => ({
    name: g.customerName.length > 16 ? `${g.customerName.slice(0, 14)}…` : g.customerName,
    Nabídky: g.quotedValue,
    Faktury: g.invoicedValue,
  }));

  return (
    <div className="space-y-6">
      <PageHeader title="Statistiky" description={`Přehled za posledních ${data.periodMonths} měsíců — zisk, náklady, doklady a vytížení.`}>
        <select
          className="rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-600 dark:bg-zinc-900"
          value={months}
          onChange={(e) => setMonths(parseInt(e.target.value, 10))}
        >
          <option value={3}>3 měsíce</option>
          <option value={6}>6 měsíců</option>
          <option value={12}>12 měsíců</option>
          <option value={24}>24 měsíců</option>
        </select>
        <button type="button" onClick={() => void load()} className={btnSecondary}>
          Obnovit
        </button>
      </PageHeader>

      {msg && <StatusBanner message={msg} variant="error" />}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard label="Zisk z kalkulací" value={fmtKc(o.calculationsProfit)} sub={`marže Ø ${o.averageMarginPercent} %`} />
        <KpiCard label="Náklady / tržby" value={fmtKc(o.calculationsCost)} sub={`tržby ${fmt(o.calculationsRevenue)} Kč`} />
        <KpiCard label="Fakturováno" value={fmtKc(o.invoicedValue)} sub={`zaplaceno ${fmt(o.paidValue)} Kč`} />
        <KpiCard label="Odhad. zisk z FA" value={fmtKc(o.invoicedEstimatedProfit)} sub={`náklady ${fmt(o.invoicedEstimatedCost)} Kč`} />
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <KpiCard label="Nabídky" value={String(o.quotesCount)} sub={fmtKc(o.quotesValue)} />
        <KpiCard label="Zakázky" value={String(o.ordersCount)} sub={fmtKc(o.ordersValue)} />
        <KpiCard label="Faktury" value={String(o.invoicesCount)} sub={`${p.invoicePaidPercent} % zaplaceno`} />
        <KpiCard label="Čas tisku" value={`${fmt(o.totalPrintHours)} h`} sub={`${o.calculationsCount} kalkulací`} />
      </div>

      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-700 dark:bg-zinc-900/60">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-sm font-medium text-zinc-800 dark:text-zinc-200">Neuhrazené faktury</h2>
            <p className="text-xs text-zinc-500">
              K likvidaci: {fmtKc(data.openInvoicesRemaining)} · všechny otevřené FA (bez ohledu na období grafů)
            </p>
          </div>
        </div>
        {data.openInvoices.length === 0 ? (
          <p className="py-4 text-center text-sm text-zinc-500">Všechny faktury jsou uhrazeny nebo zrušeny.</p>
        ) : (
          <div className={tableWrap}>
            <table className="w-full text-left text-sm">
              <thead className={tableHead}>
                <tr>
                  <th className="px-3 py-2">Číslo</th>
                  <th className="px-3 py-2">Zákazník</th>
                  <th className="px-3 py-2">Částka</th>
                  <th className="px-3 py-2">Stav</th>
                  <th className="px-3 py-2">Splatnost</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody className={tableBody}>
                {data.openInvoices.map((inv) => (
                  <tr key={inv.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                    <td className="px-3 py-2 font-mono text-zinc-500">{inv.number}</td>
                    <td className="px-3 py-2">{inv.customerName}</td>
                    <td className="px-3 py-2">{fmtKc(inv.totalAmount)}</td>
                    <td className="px-3 py-2 text-zinc-500">{STATUS_LABELS[inv.status] ?? inv.status}</td>
                    <td className="px-3 py-2 text-zinc-500">
                      {inv.dueDate ? new Date(inv.dueDate).toLocaleDateString("cs-CZ") : "—"}
                    </td>
                    <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                      <button
                        type="button"
                        className={btnPrimary + " !px-2 !py-1 text-xs"}
                        onClick={() => void markPaid(inv.id, inv.totalAmount)}
                      >
                        FA uhrazena
                      </button>
                      <Link href={`/invoices/${inv.id}`} className={linkAction}>
                        Detail
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <ChartCard title="Vývoj dokladů (Kč / měsíc)">
          <ResponsiveContainer width="100%" height={280}>
            <LineChart data={documentsMonthly}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-zinc-200 dark:stroke-zinc-700" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v as number) / 1000}k`} />
              <Tooltip formatter={(v: number) => fmtKc(v)} />
              <Legend />
              <Line type="monotone" dataKey="Nabídky" stroke="#3b82f6" strokeWidth={2} dot={false} />
              <Line type="monotone" dataKey="Zakázky" stroke="#8b5cf6" strokeWidth={2} dot={false} />
              <Line type="monotone" dataKey="Faktury" stroke="#f59e0b" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard title="Zisk vs. náklady (z kalkulací)">
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={profitVsCostMonthly}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-zinc-200 dark:stroke-zinc-700" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v as number) / 1000}k`} />
              <Tooltip formatter={(v: number) => fmtKc(v)} />
              <Legend />
              <Bar dataKey="Náklady" fill="#ef4444" radius={[4, 4, 0, 0]} />
              <Bar dataKey="Tržby" fill="#3b82f6" radius={[4, 4, 0, 0]} />
              <Bar dataKey="Zisk" fill="#10b981" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <ChartCard title="Pipeline — souhrn období (Kč)">
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={pipelineBar} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" className="stroke-zinc-200 dark:stroke-zinc-700" />
              <XAxis type="number" tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v as number) / 1000}k`} />
              <YAxis type="category" dataKey="name" width={80} tick={{ fontSize: 12 }} />
              <Tooltip formatter={(v: number) => fmtKc(v)} />
              <Bar dataKey="value" fill="#f59e0b" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
          <div className="mt-3 flex flex-wrap gap-4 text-xs text-zinc-500">
            <span>Přijetí nabídek: {p.quoteAcceptancePercent} %</span>
            <span>Dokončení zakázek: {p.orderCompletionPercent} %</span>
            <span>Úhrada faktur: {p.invoicePaidPercent} %</span>
          </div>
        </ChartCard>

        <ChartCard title="Skladba nákladů a marže (kalkulace)">
          {costPie.length === 0 ? (
            <p className="py-12 text-center text-sm text-zinc-500">Žádná data z kalkulací.</p>
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <PieChart>
                <Pie data={costPie} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={95} label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}>
                  {costPie.map((_, i) => (
                    <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v: number) => fmtKc(v)} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <ChartCard title="Top zákazníci — nabídky vs. faktury">
          {topCustomersChart.length === 0 ? (
            <p className="py-12 text-center text-sm text-zinc-500">Žádní zákazníci s fakturami.</p>
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={topCustomersChart}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-zinc-200 dark:stroke-zinc-700" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} interval={0} angle={-25} textAnchor="end" height={60} />
                <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v as number) / 1000}k`} />
                <Tooltip formatter={(v: number) => fmtKc(v)} />
                <Legend />
                <Bar dataKey="Nabídnuto" fill="#94a3b8" radius={[4, 4, 0, 0]} />
                <Bar dataKey="Fakturováno" fill="#f59e0b" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Vytížení tiskáren">
          {printerChart.length === 0 ? (
            <p className="py-12 text-center text-sm text-zinc-500">Kalkulace bez přiřazené tiskárny.</p>
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={printerChart}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-zinc-200 dark:stroke-zinc-700" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis yAxisId="left" tick={{ fontSize: 11 }} />
                <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v as number) / 1000}k`} />
                <Tooltip formatter={(v: number, name: string) => (name === "Hodiny" ? `${v} h` : fmtKc(v))} />
                <Legend />
                <Bar yAxisId="left" dataKey="Hodiny" fill="#3b82f6" radius={[4, 4, 0, 0]} />
                <Bar yAxisId="right" dataKey="Tržby" fill="#10b981" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {gapChart.length > 0 && (
        <ChartCard title="Rozdíl nabídky vs. faktury podle zákazníka">
          <p className="mb-3 text-xs text-zinc-500">
            Kladný rozdíl = fakturováno více než nabídnuto v období (nebo naopak u záporného).
          </p>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={gapChart}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-zinc-200 dark:stroke-zinc-700" />
              <XAxis dataKey="name" tick={{ fontSize: 10 }} interval={0} angle={-20} textAnchor="end" height={55} />
              <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v as number) / 1000}k`} />
              <Tooltip formatter={(v: number) => fmtKc(v)} />
              <Legend />
              <Bar dataKey="Nabídky" fill="#6366f1" radius={[4, 4, 0, 0]} />
              <Bar dataKey="Faktury" fill="#f59e0b" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      <div className="grid gap-4 md:grid-cols-3">
        {[
          { title: "Stavy nabídek", items: data.quotesByStatus },
          { title: "Stavy zakázek", items: data.ordersByStatus },
          { title: "Stavy faktur", items: data.invoicesByStatus },
        ].map(({ title, items }) => (
          <ChartCard key={title} title={title}>
            {items.length === 0 ? (
              <p className="text-sm text-zinc-500">Žádná data.</p>
            ) : (
              <ul className="space-y-2">
                {items.map((s) => (
                  <li key={s.status} className="flex items-center justify-between text-sm">
                    <span className="text-zinc-600 dark:text-zinc-400">{STATUS_LABELS[s.status] ?? s.status}</span>
                    <span className="font-medium text-zinc-900 dark:text-zinc-100">{s.count}</span>
                  </li>
                ))}
              </ul>
            )}
          </ChartCard>
        ))}
      </div>
    </div>
  );
}
