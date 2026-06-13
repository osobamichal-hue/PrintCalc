"use client";

import { useCallback, useEffect, useState } from "react";

type Health = { status: string; app: string };

export function ApiStatus() {
  const [health, setHealth] = useState<Health | null>(null);
  const [checking, setChecking] = useState(true);
  const [attempts, setAttempts] = useState(0);

  const check = useCallback(async () => {
    try {
      const r = await fetch("/api/health", { cache: "no-store" });
      if (r.ok) {
        setHealth((await r.json()) as Health);
        setChecking(false);
        return true;
      }
    } catch {
      /* API ještě startuje */
    }
    setHealth(null);
    setAttempts((a) => a + 1);
    setChecking(false);
    return false;
  }, []);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setInterval> | undefined;

    async function poll() {
      const ok = await check();
      if (cancelled) return;
      if (!ok) {
        timer = setInterval(async () => {
          const success = await check();
          if (success && timer) clearInterval(timer);
        }, 2500);
      }
    }

    void poll();
    return () => {
      cancelled = true;
      if (timer) clearInterval(timer);
    };
  }, [check]);

  if (checking && attempts === 0) {
    return (
      <div className="rounded-xl border border-zinc-200 dark:border-zinc-800/80 bg-zinc-100 dark:bg-zinc-900/50 px-5 py-4">
        <div className="flex items-center gap-3 text-sm text-zinc-500 dark:text-zinc-400">
          <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-zinc-300 dark:border-zinc-600 border-t-amber-500" />
          Připojování k backendu…
        </div>
      </div>
    );
  }

  if (health) {
    return (
      <div className="rounded-xl border border-emerald-500/30 bg-emerald-50 px-5 py-4 dark:border-emerald-500/25 dark:bg-emerald-950/20">
        <div className="flex items-center gap-3">
          <span className="flex h-3 w-3 shrink-0 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(52,211,153,0.4)] dark:bg-emerald-400" />
          <p className="text-sm text-emerald-800 dark:text-emerald-200/95">
            Backend připojen ·{" "}
            <span className="font-medium text-emerald-900 dark:text-emerald-100">{health.app}</span>
          </p>
        </div>
      </div>
    );
  }

  if (attempts < 4) {
    return (
      <div className="rounded-xl border border-zinc-200 dark:border-zinc-800/80 bg-zinc-100 dark:bg-zinc-900/50 px-5 py-4">
        <div className="flex items-center gap-3 text-sm text-zinc-500 dark:text-zinc-400">
          <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-zinc-300 dark:border-zinc-600 border-t-amber-500" />
          Čekám na spuštění API…
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-amber-400/40 bg-amber-50 px-5 py-4 text-sm leading-relaxed text-amber-900 dark:border-amber-500/30 dark:bg-amber-950/15 dark:text-amber-200/90">
      <p className="font-medium text-amber-950 dark:text-amber-100">Backend zatím neběží.</p>
      <p className="mt-2 text-amber-800 dark:text-amber-200/80">
        Z <strong>kořene</strong> projektu spusťte jedním příkazem API i web:
      </p>
      <code className="mt-2 block rounded-lg bg-amber-100 px-3 py-2 text-xs text-amber-950 dark:bg-black/30 dark:text-amber-100">
        npm run dev
      </code>
      <p className="mt-2 text-xs text-amber-700 dark:text-amber-200/60">
        Nebo dvě okna:{" "}
        <code className="rounded bg-amber-100 px-1 dark:bg-black/20">.\start-local.ps1</code>
      </p>
    </div>
  );
}
