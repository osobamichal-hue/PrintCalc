import { ApiStatus } from "@/components/ApiStatus";
import { StockAlertsPanel } from "@/components/StockAlertsPanel";
import Link from "next/link";

const modules = [
  { href: "/customers", title: "Zákazníci", desc: "Evidence a kontakty" },
  { href: "/filaments", title: "Sklad", desc: "Filamenty, příjem, výdej" },
  { href: "/calculations", title: "Kalkulace", desc: "Ceny tisku a modelování" },
  { href: "/quotes", title: "Nabídky", desc: "Z kalkulací, PDF" },
  { href: "/orders", title: "Zakázky", desc: "Z nabídek" },
  { href: "/invoices", title: "Faktury", desc: "PDF a CSV export" },
];

export default function Home() {
  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <div className="space-y-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-amber-500/90">
          Vítejte
        </p>
        <h1 className="text-balance text-3xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50 sm:text-4xl">
          PrintCalc <span className="text-amber-400">web</span>
        </h1>
        <p className="text-pretty text-sm leading-relaxed text-zinc-500 dark:text-zinc-400 sm:text-base">
          Stejná SQLite databáze jako u desktopové aplikace WPF. Všechny moduly
          jsou v levém menu — stačí spustit{" "}
          <code className="rounded bg-zinc-800 px-1.5 py-0.5 text-xs text-amber-200/90">
            npm run dev
          </code>{" "}
          z kořene projektu (API i frontend najednou).
        </p>
      </div>

      <ApiStatus />

      <StockAlertsPanel />

      <div className="grid gap-3 sm:grid-cols-2">
        {modules.map((m) => (
          <Link
            key={m.href}
            href={m.href}
            className="group rounded-lg border border-zinc-200 dark:border-zinc-800/80 bg-white dark:bg-zinc-950/50 px-4 py-3 transition-colors hover:border-amber-500/30 hover:bg-zinc-100 dark:bg-zinc-900/80"
          >
            <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500 group-hover:text-amber-500/80">
              Modul
            </div>
            <div className="mt-1 font-medium text-zinc-700 dark:text-zinc-200 group-hover:text-zinc-900 dark:text-zinc-50">
              {m.title}
            </div>
            <div className="mt-1 text-xs text-zinc-500">{m.desc}</div>
          </Link>
        ))}
      </div>
    </div>
  );
}
