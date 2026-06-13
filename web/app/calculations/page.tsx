"use client";

import { useCallback, useEffect, useState } from "react";
import { Modal } from "@/components/ui/Modal";
import { ModelPreview3D } from "@/components/ModelPreview3D";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiVoid, downloadUrl } from "@/lib/api";
import { btnPrimary, btnSecondary, labelClass, linkAction, linkDanger, linkMuted, tableBody, tableHead, tableWrap } from "@/lib/ui";
import type { Lookups } from "@/lib/types";

type CalcRow = {
  id: number;
  title: string;
  customerId: number | null;
  totalWithMargin: number;
  createdAt: string;
};

type PriceQuote = {
  printRuns: number;
  materialCost: number;
  printCost: number;
  energyCost: number;
  modelDesignCost: number;
  startFeeCost: number;
  slicingFeeCost: number;
  postProcessingCost: number;
  wasteCoefficientPercent: number;
  quantityDiscountPercent: number;
  quantityDiscountAmount: number;
  subtotal: number;
  discountedSubtotal: number;
  totalWithMargin: number;
  unitPriceForRequestedPiece: number;
};

type ModelMetadata = {
  estimatedMaterialGrams: number | null;
  estimatedPrintHours: number | null;
  bboxXmm: number | null;
  bboxYmm: number | null;
  bboxZmm: number | null;
  volumeCm3: number | null;
  warnings: string[];
};

const emptyForm = () => ({
  id: null as number | null,
  customerId: null as number | null,
  filamentTypeId: null as number | null,
  printerId: null as number | null,
  printModelId: null as number | null,
  sourceModelPath: "",
  materialGrams: 50,
  printHours: 2,
  piecesPerBuild: 1,
  requiredPieces: 1,
  marginPercent: 15,
  customerSuppliedMaterial: false,
  includeModelDesign: true,
  modelDesignHours: 0,
  modelDesignHourlyRate: 0,
  slicingFeePerModel: 100,
  postProcessingHours: 0,
  postProcessingHourlyRate: 350,
  wasteCoefficientPercent: 0,
  title: "Kalkulace",
  quotePrintDescriptionOverride: "",
});

export default function CalculationsPage() {
  const [lookups, setLookups] = useState<Lookups | null>(null);
  const [list, setList] = useState<CalcRow[]>([]);
  const [form, setForm] = useState(emptyForm());
  const [preview, setPreview] = useState<PriceQuote | null>(null);
  const [modelMeta, setModelMeta] = useState<ModelMetadata | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  const loadLookups = useCallback(async () => {
    setLookups(await apiJson<Lookups>("/api/lookups"));
  }, []);

  const loadList = useCallback(async () => {
    setList(await apiJson<CalcRow[]>("/api/calculations"));
  }, []);

  const refresh = useCallback(async () => {
    setMsg(null);
    try {
      await loadLookups();
      await loadList();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, [loadList, loadLookups]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (!form.printModelId) {
      setModelMeta(null);
      return;
    }
    const q = form.filamentTypeId ? `?filamentTypeId=${form.filamentTypeId}` : "";
    void apiJson<ModelMetadata>(`/api/print-models/${form.printModelId}/metadata${q}`)
      .then((meta) => {
        setModelMeta(meta);
        setForm((f) => ({
          ...f,
          materialGrams: meta.estimatedMaterialGrams ?? f.materialGrams,
          printHours: meta.estimatedPrintHours ?? f.printHours,
        }));
        if (meta.warnings?.length) setMsg(meta.warnings.join(" · "));
      })
      .catch(() => setModelMeta(null));
  }, [form.printModelId, form.filamentTypeId]);

  function openNewForm() {
    setForm(emptyForm());
    setPreview(null);
    setMsg(null);
    setFormOpen(true);
  }

  function closeForm() {
    setFormOpen(false);
    setPreview(null);
  }

  function bodyFromForm() {
    return {
      customerId: form.customerId,
      filamentTypeId: form.filamentTypeId,
      printerId: form.printerId,
      printModelId: form.printModelId,
      sourceModelPath: form.sourceModelPath || null,
      materialGrams: form.materialGrams,
      printHours: form.printHours,
      piecesPerBuild: form.piecesPerBuild,
      requiredPieces: form.requiredPieces,
      marginPercent: form.marginPercent,
      customerSuppliedMaterial: form.customerSuppliedMaterial,
      includeModelDesign: form.includeModelDesign,
      modelDesignHours: form.modelDesignHours,
      modelDesignHourlyRate: form.modelDesignHourlyRate,
      slicingFeePerModel: form.slicingFeePerModel,
      postProcessingHours: form.postProcessingHours,
      postProcessingHourlyRate: form.postProcessingHourlyRate,
      wasteCoefficientPercent: form.wasteCoefficientPercent,
      title: form.title,
      quotePrintDescriptionOverride: form.quotePrintDescriptionOverride || null,
    };
  }

  async function runPreview() {
    setMsg(null);
    try {
      const q = await apiJson<PriceQuote>("/api/calculations/preview", {
        method: "POST",
        body: JSON.stringify(bodyFromForm()),
      });
      setPreview(q);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
      setPreview(null);
    }
  }

  async function issueStockFromCalc() {
    if (!form.id) return;
    setMsg(null);
    try {
      await apiVoid(`/api/calculations/${form.id}/issue-stock`, { method: "POST" });
      setMsg("Materiál byl vydán ze skladu.");
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function save() {
    setMsg(null);
    try {
      if (form.id) {
        await apiVoid(`/api/calculations/${form.id}`, {
          method: "PUT",
          body: JSON.stringify(bodyFromForm()),
        });
      } else {
        await apiJson("/api/calculations", {
          method: "POST",
          body: JSON.stringify(bodyFromForm()),
        });
      }
      closeForm();
      setForm(emptyForm());
      await loadList();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function loadOne(id: number) {
    setMsg(null);
    try {
      const c = await apiJson<Record<string, unknown>>(`/api/calculations/${id}`);
      setForm({
        id: c.id as number,
        customerId: (c.customerId as number) ?? null,
        filamentTypeId: (c.filamentTypeId as number) ?? null,
        printerId: (c.printerId as number) ?? null,
        printModelId: (c.printModelId as number) ?? null,
        sourceModelPath: (c.sourceModelPath as string) ?? "",
        materialGrams: Number(c.materialGrams),
        printHours: Number(c.printHours),
        piecesPerBuild: Number(c.piecesPerBuild),
        requiredPieces: Number(c.requiredPieces),
        marginPercent: Number(c.marginPercent),
        customerSuppliedMaterial: Boolean(c.customerSuppliedMaterial),
        includeModelDesign: Boolean(c.includeModelDesign),
        modelDesignHours: Number(c.modelDesignHours),
        modelDesignHourlyRate: Number(c.modelDesignHourlyRate),
        slicingFeePerModel: Number(c.slicingFeePerModel ?? 100),
        postProcessingHours: Number(c.postProcessingHours ?? 0),
        postProcessingHourlyRate: Number(c.postProcessingHourlyRate ?? 350),
        wasteCoefficientPercent: Number(c.wasteCoefficientPercent ?? 0),
        title: String(c.title),
        quotePrintDescriptionOverride: String(c.quotePrintDescriptionOverride ?? ""),
      });
      setPreview(null);
      setFormOpen(true);
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  async function del(id: number) {
    if (!confirm("Smazat kalkulaci?")) return;
    try {
      await apiVoid(`/api/calculations/${id}`, { method: "DELETE" });
      if (form.id === id) closeForm();
      await loadList();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }

  if (!lookups) return <p className="text-zinc-500">Načítám…</p>;

  return (
    <div className="space-y-4">
      <PageHeader title="Kalkulace" description="Seznam uložených kalkulací.">
        <button type="button" onClick={openNewForm} className={btnPrimary}>
          + Nová kalkulace
        </button>
      </PageHeader>

      {msg && !formOpen && <StatusBanner message={msg} />}

      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Název</th>
              <th className="px-3 py-2">Celkem</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {list.length === 0 && (
              <tr>
                <td colSpan={3} className="px-3 py-8 text-center text-zinc-500">
                  Zatím žádné kalkulace.{" "}
                  <button
                    type="button"
                    className="text-amber-600 hover:underline dark:text-amber-400"
                    onClick={openNewForm}
                  >
                    Vytvořit první
                  </button>
                </td>
              </tr>
            )}
            {list.map((c) => (
              <tr key={c.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                <td className="px-3 py-2 font-medium text-zinc-800 dark:text-zinc-200">{c.title}</td>
                <td className="px-3 py-2 text-zinc-600 dark:text-zinc-400">{c.totalWithMargin} Kč</td>
                <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                  <button
                    type="button"
                    className={linkAction}
                    onClick={() => void loadOne(c.id)}
                  >
                    Upravit
                  </button>
                  <a
                    href={downloadUrl(`/api/calculations/${c.id}/pdf`)}
                    target="_blank"
                    rel="noreferrer"
                    className={linkMuted}
                  >
                    PDF
                  </a>
                  <button
                    type="button"
                    className={linkDanger}
                    onClick={() => void del(c.id)}
                  >
                    Smazat
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Modal
        open={formOpen}
        onClose={closeForm}
        title={form.id ? `Upravit kalkulaci #${form.id}` : "Nová kalkulace"}
        footer={
          <>
            <button type="button" onClick={closeForm} className={btnSecondary}>Zrušit</button>
            {form.id && !form.customerSuppliedMaterial && form.filamentTypeId && (
              <button type="button" onClick={() => void issueStockFromCalc()} className="rounded-md bg-red-700 px-4 py-2 text-sm font-medium text-white hover:bg-red-600">
                Vydat ze skladu
              </button>
            )}
            <button type="button" onClick={() => void runPreview()} className={btnSecondary}>Přepočítat</button>
            <button type="button" onClick={() => void save()} className={btnPrimary}>Uložit</button>
          </>
        }
      >
            {msg && formOpen && <StatusBanner message={msg} />}

            <div className="grid gap-2 sm:grid-cols-2">
              <label className={labelClass}>
                Zákazník
                <select
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.customerId ?? ""}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      customerId: e.target.value ? parseInt(e.target.value, 10) : null,
                    }))
                  }
                >
                  <option value="">—</option>
                  {lookups.customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-xs text-zinc-500">
                Filament
                <select
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.filamentTypeId ?? ""}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      filamentTypeId: e.target.value ? parseInt(e.target.value, 10) : null,
                    }))
                  }
                >
                  <option value="">—</option>
                  {lookups.filamentTypes.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-xs text-zinc-500">
                Tiskárna
                <select
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.printerId ?? ""}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      printerId: e.target.value ? parseInt(e.target.value, 10) : null,
                    }))
                  }
                >
                  <option value="">—</option>
                  {lookups.printers.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-xs text-zinc-500">
                Model (DB)
                <select
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.printModelId ?? ""}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      printModelId: e.target.value ? parseInt(e.target.value, 10) : null,
                    }))
                  }
                >
                  <option value="">—</option>
                  {lookups.printModels.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            {form.printModelId && (
              <div className="mt-3 space-y-2">
                <ModelPreview3D
                  modelId={form.printModelId}
                  fileType={lookups.printModels.find((m) => m.id === form.printModelId)?.fileType}
                />
                {modelMeta && (modelMeta.bboxXmm || modelMeta.volumeCm3) && (
                  <p className="text-xs text-zinc-500">
                    {modelMeta.bboxXmm
                      ? `Rozměry: ${modelMeta.bboxXmm}×${modelMeta.bboxYmm}×${modelMeta.bboxZmm} mm`
                      : ""}
                    {modelMeta.volumeCm3 ? ` · Objem: ${modelMeta.volumeCm3} cm³` : ""}
                  </p>
                )}
              </div>
            )}

            <input
              placeholder="Název kalkulace"
              className="mt-3 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
            />

            <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
              <label className="text-xs text-zinc-500">
                Materiál (g)
                <input
                  type="number"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.materialGrams}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      materialGrams: parseFloat(e.target.value) || 0,
                    }))
                  }
                />
              </label>
              <label className="text-xs text-zinc-500">
                Čas tisku (h)
                <input
                  type="number"
                  step="0.01"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.printHours}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      printHours: parseFloat(e.target.value) || 0,
                    }))
                  }
                />
              </label>
              <label className="text-xs text-zinc-500">
                ks / podložka
                <input
                  type="number"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.piecesPerBuild}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      piecesPerBuild: parseInt(e.target.value, 10) || 1,
                    }))
                  }
                />
              </label>
              <label className="text-xs text-zinc-500">
                Požadováno ks
                <input
                  type="number"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.requiredPieces}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      requiredPieces: parseInt(e.target.value, 10) || 1,
                    }))
                  }
                />
              </label>
            </div>

            <label className="mt-3 block text-xs text-zinc-500">
              Marže %
              <input
                type="number"
                className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                value={form.marginPercent}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    marginPercent: parseFloat(e.target.value) || 0,
                  }))
                }
              />
            </label>

            <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">
              <label className="text-xs text-zinc-500">
                Slicing fee (Kč)
                <input
                  type="number"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.slicingFeePerModel}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      slicingFeePerModel: parseFloat(e.target.value) || 0,
                    }))
                  }
                />
              </label>
              <label className="text-xs text-zinc-500">
                Zmetkovitost (%)
                <input
                  type="number"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.wasteCoefficientPercent}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      wasteCoefficientPercent: parseFloat(e.target.value) || 0,
                    }))
                  }
                />
              </label>
              <label className="text-xs text-zinc-500">
                Post-proc. (h)
                <input
                  type="number"
                  step="0.01"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.postProcessingHours}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      postProcessingHours: parseFloat(e.target.value) || 0,
                    }))
                  }
                />
              </label>
              <label className="text-xs text-zinc-500">
                Post-proc. Kč/h
                <input
                  type="number"
                  className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                  value={form.postProcessingHourlyRate}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      postProcessingHourlyRate: parseFloat(e.target.value) || 0,
                    }))
                  }
                />
              </label>
            </div>

            <div className="mt-3 flex flex-wrap gap-4 text-sm text-zinc-700 dark:text-zinc-300">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={form.customerSuppliedMaterial}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      customerSuppliedMaterial: e.target.checked,
                    }))
                  }
                />
                Materiál zákazníka
              </label>
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={form.includeModelDesign}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, includeModelDesign: e.target.checked }))
                  }
                />
                Modelování
              </label>
            </div>

            {form.includeModelDesign && (
              <div className="mt-3 grid grid-cols-2 gap-2">
                <label className="text-xs text-zinc-500">
                  Modelování (h)
                  <input
                    type="number"
                    step="0.01"
                    className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                    value={form.modelDesignHours}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        modelDesignHours: parseFloat(e.target.value) || 0,
                      }))
                    }
                  />
                </label>
                <label className="text-xs text-zinc-500">
                  Kč/h modelování (0 = výchozí)
                  <input
                    type="number"
                    className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
                    value={form.modelDesignHourlyRate}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        modelDesignHourlyRate: parseFloat(e.target.value) || 0,
                      }))
                    }
                  />
                </label>
              </div>
            )}

            {preview && (
              <div className="mt-4 rounded border border-zinc-300 bg-zinc-50 p-3 text-sm text-zinc-700 dark:border-zinc-700 dark:bg-zinc-950/80 dark:text-zinc-300">
                <div>Celkem: {preview.totalWithMargin} Kč</div>
                <div>za ks: {preview.unitPriceForRequestedPiece} Kč</div>
                <div className="mt-2 text-xs text-zinc-500">
                  Tisků: {preview.printRuns} · Materiál {preview.materialCost}
                  {preview.wasteCoefficientPercent > 0 ? ` (vč. ${preview.wasteCoefficientPercent}% waste)` : ""}
                  {" · "}Tisk {preview.printCost} · Energie {preview.energyCost} · Model{" "}
                  {preview.modelDesignCost} · Start {preview.startFeeCost} · Slicing{" "}
                  {preview.slicingFeeCost} · Post-proc. {preview.postProcessingCost}
                  {preview.quantityDiscountAmount > 0
                    ? ` · Sleva -${preview.quantityDiscountAmount} (${preview.quantityDiscountPercent}%)`
                    : ""}
                </div>
              </div>
            )}

      </Modal>
    </div>
  );
}
