"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiForm } from "@/lib/api";
import { btnPrimary, btnSecondary } from "@/lib/ui";

type Detail = { id: number };

export default function ImportPurchaseInvoicePage() {
  const router = useRouter();
  const [file, setFile] = useState<File | null>(null);
  const [drag, setDrag] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onDrop = useCallback((f: File) => {
    setFile(f);
    setMsg(null);
  }, []);

  async function upload() {
    if (!file) {
      setMsg("Vyberte soubor.");
      return;
    }
    setBusy(true);
    setMsg(null);
    try {
      const fd = new FormData();
      fd.append("file", file);
      const created = await apiForm<Detail>("/api/purchase-invoices/import", fd);
      router.push(`/purchase-invoices/${created.id}`);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba importu");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Import přijaté faktury"
        description="Podporované formáty: ISDOC, XML, CSV, Excel (.xlsx), PDF (Gemini AI + offline fallback)."
      />
      {msg && <StatusBanner message={msg} />}

      <div
        className={`rounded-lg border-2 border-dashed p-10 text-center transition-colors ${
          drag ? "border-amber-500 bg-amber-50/50 dark:bg-amber-950/20" : "border-zinc-300 dark:border-zinc-700"
        }`}
        onDragOver={(e) => {
          e.preventDefault();
          setDrag(true);
        }}
        onDragLeave={() => setDrag(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDrag(false);
          const f = e.dataTransfer.files[0];
          if (f) onDrop(f);
        }}
      >
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          Přetáhněte soubor sem nebo{" "}
          <label className="cursor-pointer text-sky-600 hover:underline dark:text-sky-400">
            vyberte ze disku
            <input
              type="file"
              className="hidden"
              accept=".pdf,.xml,.isdoc,.csv,.xlsx,.xls,.txt"
              onChange={(e) => {
                const f = e.target.files?.[0];
                if (f) onDrop(f);
              }}
            />
          </label>
        </p>
        {file && (
          <p className="mt-3 text-sm font-medium text-zinc-800 dark:text-zinc-200">
            {file.name} ({Math.round(file.size / 1024)} KB)
          </p>
        )}
      </div>

      <div className="flex gap-2">
        <button type="button" className={btnPrimary} disabled={busy || !file} onClick={() => void upload()}>
          {busy ? "Importuji…" : "Importovat a párovat"}
        </button>
        <Link href="/purchase-invoices" className={btnSecondary}>
          Zpět
        </Link>
      </div>
    </div>
  );
}
