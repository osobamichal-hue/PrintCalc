"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiUrl, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, linkAction, linkDanger, linkMuted, tableBody, tableHead, tableWrap } from "@/lib/ui";

type ModelRow = {
  id: number;
  name: string;
  fileType: string;
  originalFileName: string;
  estimatedMaterialGrams: number | null;
  estimatedPrintHours: number | null;
  createdAt: string;
};

export default function ModelsPage() {
  const [items, setItems] = useState<ModelRow[]>([]);
  const [msg, setMsg] = useState<string | null>(null);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setMsg(null);
    try {
      setItems(await apiJson<ModelRow[]>("/api/print-models"));
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function upload(file: File) {
    setBusy(true);
    setMsg(null);
    try {
      const fd = new FormData();
      fd.append("file", file);
      const r = await fetch(apiUrl("/api/print-models"), { method: "POST", body: fd });
      if (!r.ok) throw new Error(await r.text());
      setUploadOpen(false);
      await load();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: number) {
    if (!confirm("Smazat model?")) return;
    try {
      await apiVoid(`/api/print-models/${id}`, { method: "DELETE" });
      await load();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Modely" description="STL, 3MF a GCode s metadaty ze sliceru.">
        <button type="button" onClick={() => setUploadOpen(true)} className={btnPrimary}>
          + Nahrát model
        </button>
      </PageHeader>

      {msg && !uploadOpen && <StatusBanner message={msg} />}

      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Název</th>
              <th className="px-3 py-2">Soubor</th>
              <th className="px-3 py-2">g / h</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {items.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-3 py-8 text-center text-zinc-500">
                  Žádné modely.{" "}
                  <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={() => setUploadOpen(true)}>
                    Nahrát první
                  </button>
                </td>
              </tr>
            ) : (
              items.map((m) => (
                <tr key={m.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                  <td className="px-3 py-2 font-medium text-zinc-800 dark:text-zinc-200">{m.name}</td>
                  <td className="px-3 py-2 text-zinc-500">
                    {m.originalFileName} ({m.fileType})
                  </td>
                  <td className="px-3 py-2 text-zinc-500">
                    {m.estimatedMaterialGrams ?? "—"} g / {m.estimatedPrintHours ?? "—"} h
                  </td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    <a href={apiUrl(`/api/print-models/${m.id}/file`)} download className={linkMuted}>
                      Stáhnout
                    </a>
                    <button type="button" className={linkDanger} onClick={() => void remove(m.id)}>
                      Smazat
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <Modal
        open={uploadOpen}
        onClose={() => !busy && setUploadOpen(false)}
        title="Nahrát model"
        footer={
          <>
            <button type="button" disabled={busy} onClick={() => setUploadOpen(false)} className={btnSecondary}>
              Zrušit
            </button>
            <button type="button" disabled={busy} onClick={() => fileRef.current?.click()} className={btnPrimary}>
              {busy ? "Nahrávám…" : "Vybrat soubor…"}
            </button>
          </>
        }
      >
        {msg && uploadOpen && <StatusBanner message={msg} />}
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          Podporované formáty: STL, 3MF, GCode. U 3MF a GCode se automaticky načtou čas tisku a spotřeba materiálu.
        </p>
        <input
          ref={fileRef}
          type="file"
          accept=".stl,.3mf,.gcode,.gco"
          className="hidden"
          disabled={busy}
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = "";
            if (file) void upload(file);
          }}
        />
      </Modal>
    </div>
  );
}
