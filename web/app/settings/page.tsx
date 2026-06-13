"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiForm, apiJson, apiVoid, downloadFile } from "@/lib/api";
import { btnPrimary, btnSecondary } from "@/lib/ui";
import type { AppSettingRow } from "@/lib/types";

type RestoreResponse = {
  message: string;
  manifest?: {
    createdAt: string;
    databaseSizeBytes: number;
    customers: number;
    calculations: number;
    quotes: number;
    invoices: number;
  };
};

export default function SettingsPage() {
  const [rows, setRows] = useState<AppSettingRow[]>([]);
  const [msg, setMsg] = useState<string | null>(null);
  const [msgVariant, setMsgVariant] = useState<"warning" | "error" | "info" | "success">("info");
  const [backupBusy, setBackupBusy] = useState(false);
  const [restoreBusy, setRestoreBusy] = useState(false);
  const restoreInputRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      setRows(await apiJson<AppSettingRow[]>("/api/settings"));
    } catch (e) {
      setMsgVariant("error");
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function save() {
    setMsg(null);
    try {
      await apiVoid("/api/settings", {
        method: "PUT",
        body: JSON.stringify(rows),
      });
      setMsgVariant("success");
      setMsg("Nastavení uloženo.");
      await load();
    } catch (e) {
      setMsgVariant("error");
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function createBackup() {
    setBackupBusy(true);
    setMsg(null);
    try {
      const stamp = new Date().toISOString().slice(0, 19).replace(/[-:T]/g, "").replace(" ", "_");
      await downloadFile("/api/backup/download", `PrintCalc_Backup_${stamp}.zip`);
      setMsgVariant("success");
      setMsg("Záloha byla stažena. Uložte soubor ZIP na bezpečné místo.");
    } catch (e) {
      setMsgVariant("error");
      setMsg(e instanceof Error ? e.message : "Záloha selhala");
    } finally {
      setBackupBusy(false);
    }
  }

  async function restoreBackup(file: File) {
    if (
      !confirm(
        "Obnovit ze zálohy přepíše aktuální databázi a datovou složku aplikace.\n\nPokračovat?"
      )
    ) {
      return;
    }
    setRestoreBusy(true);
    setMsg(null);
    try {
      const fd = new FormData();
      fd.append("file", file);
      const res = await apiForm<RestoreResponse>("/api/backup/restore", fd);
      setMsgVariant("success");
      const m = res.manifest;
      const detail = m
        ? ` Záloha z ${new Date(m.createdAt).toLocaleString("cs-CZ")} — ${m.customers} zákazníků, ${m.calculations} kalkulací, ${m.invoices} faktur.`
        : "";
      setMsg(res.message + detail);
      await load();
    } catch (e) {
      setMsgVariant("error");
      setMsg(e instanceof Error ? e.message : "Obnova selhala");
    } finally {
      setRestoreBusy(false);
      if (restoreInputRef.current) restoreInputRef.current.value = "";
    }
  }

  function setVal(key: string, value: string) {
    setRows((r) => {
      if (r.some((x) => x.key === key)) {
        return r.map((x) => (x.key === key ? { ...x, value } : x));
      }
      return [...r, { key, value }];
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Nastavení"
        description="Firemní údaje, AI import, záloha dat a klíče v databázi."
      />

      {msg && <StatusBanner message={msg} variant={msgVariant} />}

      <section className="rounded-xl border border-zinc-200 bg-white p-5 dark:border-zinc-700 dark:bg-zinc-900/60">
        <h2 className="text-sm font-semibold text-zinc-800 dark:text-zinc-200">Záloha a obnova</h2>
        <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
          Kompletní ZIP záloha obsahuje databázi (<code className="text-xs">printcalc.db</code>),
          všechna nastavení z aplikace, export datové složky (modely, PDF, exporty) a manifest se
          souhrnem. Formát je kompatibilní se zálohou z desktopové WPF aplikace.
        </p>
        <ul className="mt-2 list-inside list-disc text-xs text-zinc-500">
          <li>Databáze — zákazníci, kalkulace, nabídky, zakázky, faktury, sklad</li>
          <li>Nastavení — klíče z tabulky AppSettings (firma, DPH, cesty, AI…)</li>
          <li>Datová složka — soubory podle <code>App.DataRootPath</code> (bez podsložky Backups)</li>
        </ul>
        <div className="mt-4 flex flex-wrap gap-3">
          <button
            type="button"
            disabled={backupBusy || restoreBusy}
            onClick={() => void createBackup()}
            className={btnPrimary}
          >
            {backupBusy ? "Vytvářím zálohu…" : "Stáhnout kompletní zálohu (ZIP)"}
          </button>
          <button
            type="button"
            disabled={backupBusy || restoreBusy}
            onClick={() => restoreInputRef.current?.click()}
            className={btnSecondary}
          >
            {restoreBusy ? "Obnovuji…" : "Obnovit ze zálohy…"}
          </button>
          <input
            ref={restoreInputRef}
            type="file"
            accept=".zip,application/zip"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void restoreBackup(file);
            }}
          />
        </div>
        <p className="mt-3 text-xs text-amber-700 dark:text-amber-300/90">
          Po obnově doporučujeme restartovat backend (<code>npm run dev</code>) a obnovit stránku v
          prohlížeči.
        </p>
      </section>

      <section className="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
        <h2 className="text-sm font-semibold text-zinc-800 dark:text-zinc-200">AI / Import přijatých FA</h2>
        <p className="mt-1 text-xs text-zinc-500">
          Gemini API klíč (free tier), volitelný OpenAI-compatible fallback (Ollama) a prahy auto-párování.
        </p>
        <div className="mt-3 grid gap-2 md:grid-cols-2">
          {["Ai.Gemini.ApiKey", "Ai.Gemini.Model", "Ai.Fallback.Endpoint", "Ai.Fallback.Model", "PurchaseInvoice.MatchAutoThreshold", "PurchaseInvoice.MatchSuggestThreshold"].map((key) => {
            const row = rows.find((x) => x.key === key) ?? { key, value: "" };
            return (
              <label key={key} className="block text-xs text-zinc-500">
                {key}
                <input
                  className="mt-1 w-full rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm"
                  type={key.includes("ApiKey") ? "password" : "text"}
                  value={row.value}
                  onChange={(e) => setVal(key, e.target.value)}
                />
              </label>
            );
          })}
        </div>
      </section>
      <button
        type="button"
        onClick={() => void save()}
        className={btnPrimary}
      >
        Uložit vše
      </button>
      <div className="max-h-[calc(100vh-12rem)] overflow-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="sticky top-0 border-b border-zinc-200 dark:border-zinc-800 bg-zinc-100 dark:bg-zinc-900 text-xs uppercase text-zinc-500">
            <tr>
              <th className="px-3 py-2">Klíč</th>
              <th className="px-3 py-2">Hodnota</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
            {rows.map((row) => (
              <tr key={row.key}>
                <td className="whitespace-nowrap px-3 py-1 font-mono text-xs text-zinc-500">
                  {row.key}
                </td>
                <td className="px-2 py-1">
                  <input
                    className="w-full min-w-[12rem] rounded border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-2 py-1 text-sm text-zinc-900 dark:text-zinc-100"
                    value={row.value}
                    onChange={(e) => setVal(row.key, e.target.value)}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
