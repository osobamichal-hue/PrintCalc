"use client";

import { useCallback, useEffect, useState } from "react";
import { apiJson, apiVoid } from "@/lib/api";
import type { AppSettingRow } from "@/lib/types";

export default function SettingsPage() {
  const [rows, setRows] = useState<AppSettingRow[]>([]);
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      setRows(await apiJson<AppSettingRow[]>("/api/settings"));
    } catch (e) {
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
      setMsg("Uloženo.");
      await load();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
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
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Nastavení</h1>
        <p className="mt-1 text-sm text-zinc-500">
          Úprava klíč–hodnota v databázi (stejné jako firemní nastavení ve WPF).
        </p>
      </div>
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
      {msg && (
        <div className="rounded border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-900/80 px-3 py-2 text-sm text-zinc-600 dark:text-zinc-300">
          {msg}
        </div>
      )}
      <button
        type="button"
        onClick={() => void save()}
        className="rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-zinc-900 dark:text-zinc-950"
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
