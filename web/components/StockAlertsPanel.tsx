"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { apiJson } from "@/lib/api";

type StockAlert = {
  kind: string;
  filamentTypeId: number;
  filamentTypeName: string;
  currentKg: number;
  minStockKg: number | null;
  filamentStockId: number | null;
  lotNumber: string | null;
  expirationDate: string | null;
  message: string;
};

export function StockAlertsPanel() {
  const [alerts, setAlerts] = useState<StockAlert[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setAlerts(await apiJson<StockAlert[]>("/api/stock/alerts"));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (error) return null;
  if (alerts.length === 0) return null;

  return (
    <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 px-4 py-3">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-amber-700 dark:text-amber-400">
          Skladové upozornění ({alerts.length})
        </h2>
        <Link href="/filaments" className="text-xs text-amber-600 hover:underline dark:text-amber-400">
          Otevřít sklad
        </Link>
      </div>
      <ul className="mt-2 space-y-1 text-sm text-zinc-600 dark:text-zinc-400">
        {alerts.slice(0, 5).map((a, i) => (
          <li key={`${a.kind}-${a.filamentTypeId}-${a.filamentStockId ?? i}`}>
            <span className={a.kind === "LowStock" ? "text-red-600 dark:text-red-400" : "text-amber-600 dark:text-amber-400"}>
              {a.kind === "LowStock" ? "Nízká zásoba" : "Expirace"}
            </span>
            {" — "}
            {a.message}
          </li>
        ))}
        {alerts.length > 5 && (
          <li className="text-xs text-zinc-500">… a dalších {alerts.length - 5}</li>
        )}
      </ul>
    </div>
  );
}
