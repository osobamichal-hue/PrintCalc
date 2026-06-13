"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ModelPreview3D } from "@/components/ModelPreview3D";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiUrl, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, linkDanger, linkMuted, tableBody, tableHead, tableWrap } from "@/lib/ui";

type ModelRow = {
  id: number;
  name: string;
  fileType: string;
  originalFileName: string;
  estimatedMaterialGrams: number | null;
  estimatedPrintHours: number | null;
  volumeCm3: number | null;
  bboxXmm: number | null;
  bboxYmm: number | null;
  bboxZmm: number | null;
  estimateSource: string;
  geometryWarnings: string | null;
  createdAt: string;
};

export default function ModelsPage() {
  const [items, setItems] = useState<ModelRow[]>([]);
  const [msg, setMsg] = useState<string | null>(null);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [previewId, setPreviewId] = useState<number | null>(null);
  const [previewMeta, setPreviewMeta] = useState<string | null>(null);
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

  useEffect(() => {
    if (previewId === null) {
      setPreviewMeta(null);
      return;
    }
    void (async () => {
      try {
        const meta = await apiJson<{
          estimatedMaterialGrams: number | null;
          estimatedPrintHours: number | null;
          volumeCm3: number | null;
          bboxXmm: number | null;
          bboxYmm: number | null;
          bboxZmm: number | null;
          estimateSource: string;
          warnings: string[];
        }>(`/api/print-models/${previewId}/metadata`);
        const lines = [
          meta.estimatedMaterialGrams != null ? `${meta.estimatedMaterialGrams} g` : null,
          meta.estimatedPrintHours != null ? `${meta.estimatedPrintHours} h` : null,
          meta.bboxXmm ? `${meta.bboxXmm}×${meta.bboxYmm}×${meta.bboxZmm} mm` : null,
          meta.volumeCm3 ? `${meta.volumeCm3} cm³` : null,
          meta.estimateSource,
          ...meta.warnings,
        ].filter(Boolean);
        setPreviewMeta(lines.join(" · "));
      } catch {
        const m = items.find((x) => x.id === previewId);
        setPreviewMeta(m?.geometryWarnings ?? null);
      }
    })();
  }, [previewId, items]);

  async function reanalyze(id: number) {
    setBusy(true);
    setMsg(null);
    try {
      await apiJson(`/api/print-models/${id}/reanalyze`, { method: "POST" });
      await load();
      if (previewId === id) setPreviewId(id);
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    } finally {
      setBusy(false);
    }
  }

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
      if (previewId === id) setPreviewId(null);
      await load();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  const previewModel = previewId ? items.find((m) => m.id === previewId) : null;

  return (
    <div className="space-y-4">
      <PageHeader title="Modely" description="STL, OBJ, 3MF a GCode — hybridní odhad ze sliceru nebo geometrie.">
        <button type="button" onClick={() => setUploadOpen(true)} className={btnPrimary}>
          + Nahrát model
        </button>
      </PageHeader>

      {msg && !uploadOpen && !previewId && <StatusBanner message={msg} />}

      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Název</th>
              <th className="px-3 py-2">Soubor</th>
              <th className="px-3 py-2">g / h</th>
              <th className="px-3 py-2">Geometrie</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-zinc-500">
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
                    <div className="text-xs">{m.estimateSource}</div>
                  </td>
                  <td className="px-3 py-2 text-zinc-500">
                    {m.estimatedMaterialGrams ?? "—"} g / {m.estimatedPrintHours ?? "—"} h
                  </td>
                  <td className="px-3 py-2 text-xs text-zinc-500">
                    {m.bboxXmm
                      ? `${m.bboxXmm}×${m.bboxYmm}×${m.bboxZmm} mm`
                      : "—"}
                    {m.volumeCm3 ? ` · ${m.volumeCm3} cm³` : ""}
                  </td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    <button type="button" className={linkMuted} onClick={() => void reanalyze(m.id)}>
                      Přeanalyzovat
                    </button>
                    <button type="button" className={linkMuted} onClick={() => setPreviewId(m.id)}>
                      Náhled
                    </button>
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
          STL, OBJ, 3MF, GCode. Primárně metadata ze sliceru; u STL/OBJ fallback odhad hmotnosti z objemu.
        </p>
        <input
          ref={fileRef}
          type="file"
          accept=".stl,.obj,.3mf,.gcode,.gco"
          className="hidden"
          disabled={busy}
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = "";
            if (file) void upload(file);
          }}
        />
      </Modal>

      <Modal
        open={previewId !== null}
        onClose={() => setPreviewId(null)}
        title={previewModel ? `Náhled: ${previewModel.name}` : "Náhled modelu"}
        footer={
          <button type="button" onClick={() => setPreviewId(null)} className={btnSecondary}>
            Zavřít
          </button>
        }
      >
        {previewModel && (
          <div className="space-y-3">
            <ModelPreview3D modelId={previewModel.id} fileType={previewModel.fileType} />
            {previewMeta && (
              <p className="text-sm text-zinc-600 dark:text-zinc-400">{previewMeta}</p>
            )}
            {previewModel.geometryWarnings && !previewMeta && (
              <p className="text-xs text-amber-600 dark:text-amber-400">{previewModel.geometryWarnings}</p>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
}
