"use client";

import { useCallback, useEffect, useState } from "react";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, inputClass, labelClass, linkAction, linkDanger, tableBody, tableHead, tableWrap } from "@/lib/ui";

type Printer = {
  id: number;
  name: string;
  kind: string;
  hourlyRate: number;
  kwhPerHour: number;
  startFeePerPrint: number;
  maxVolumeDescription: string | null;
  notes: string | null;
};

const kinds = ["Fff", "Sla"] as const;

const emptyForm = () => ({
  name: "",
  kind: "Fff",
  hourlyRate: 120,
  kwhPerHour: 0.08,
  startFeePerPrint: 15,
  maxVolumeDescription: "",
  notes: "",
});

export default function PrintersPage() {
  const [items, setItems] = useState<Printer[]>([]);
  const [msg, setMsg] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState(emptyForm());

  const load = useCallback(async () => {
    setMsg(null);
    try {
      setItems(await apiJson<Printer[]>("/api/printers"));
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function openNew() {
    setEditId(null);
    setForm(emptyForm());
    setFormOpen(true);
  }

  function openEdit(p: Printer) {
    setEditId(p.id);
    setForm({
      name: p.name,
      kind: p.kind,
      hourlyRate: p.hourlyRate,
      kwhPerHour: p.kwhPerHour,
      startFeePerPrint: p.startFeePerPrint,
      maxVolumeDescription: p.maxVolumeDescription ?? "",
      notes: p.notes ?? "",
    });
    setFormOpen(true);
  }

  function closeForm() {
    setFormOpen(false);
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setMsg(null);
    const body = {
      ...form,
      maxVolumeDescription: form.maxVolumeDescription || null,
      notes: form.notes || null,
    };
    try {
      if (editId) {
        await apiVoid(`/api/printers/${editId}`, {
          method: "PUT",
          body: JSON.stringify(body),
        });
      } else {
        await apiJson("/api/printers", { method: "POST", body: JSON.stringify(body) });
      }
      closeForm();
      await load();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  async function remove(id: number) {
    if (!confirm("Smazat tiskárnu?")) return;
    try {
      await apiVoid(`/api/printers/${id}`, { method: "DELETE" });
      await load();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Tiskárny" description="Evidence tiskáren, sazeb a spotřeby.">
        <button type="button" onClick={openNew} className={btnPrimary}>
          + Nová tiskárna
        </button>
      </PageHeader>

      {msg && !formOpen && <StatusBanner message={msg} />}

      <div className={tableWrap}>
        <table className="w-full text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Název</th>
              <th className="px-3 py-2">Typ</th>
              <th className="px-3 py-2">Kč/h</th>
              <th className="px-3 py-2">Start</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-zinc-500">
                  Žádné tiskárny.{" "}
                  <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={openNew}>
                    Přidat
                  </button>
                </td>
              </tr>
            ) : (
              items.map((p) => (
                <tr key={p.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                  <td className="px-3 py-2 font-medium text-zinc-800 dark:text-zinc-200">{p.name}</td>
                  <td className="px-3 py-2 text-zinc-500">{p.kind}</td>
                  <td className="px-3 py-2 text-zinc-500">{p.hourlyRate}</td>
                  <td className="px-3 py-2 text-zinc-500">{p.startFeePerPrint}</td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    <button type="button" className={linkAction} onClick={() => openEdit(p)}>
                      Upravit
                    </button>
                    <button type="button" className={linkDanger} onClick={() => void remove(p.id)}>
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
        open={formOpen}
        onClose={closeForm}
        title={editId ? "Upravit tiskárnu" : "Nová tiskárna"}
        footer={
          <>
            <button type="button" onClick={closeForm} className={btnSecondary}>
              Zrušit
            </button>
            <button type="submit" form="printer-form" className={btnPrimary}>
              Uložit
            </button>
          </>
        }
      >
        {msg && formOpen && <StatusBanner message={msg} />}
        <form id="printer-form" onSubmit={(e) => void save(e)} className="mt-3 grid gap-3 sm:grid-cols-2">
          <label className={labelClass}>
            Název *
            <input className={inputClass} value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
          </label>
          <label className={labelClass}>
            Typ
            <select className={inputClass} value={form.kind} onChange={(e) => setForm((f) => ({ ...f, kind: e.target.value }))}>
              {kinds.map((k) => (
                <option key={k} value={k}>{k}</option>
              ))}
            </select>
          </label>
          <label className={labelClass}>
            Hodinovka (Kč/h)
            <input type="number" step="0.01" className={inputClass} value={form.hourlyRate} onChange={(e) => setForm((f) => ({ ...f, hourlyRate: parseFloat(e.target.value) || 0 }))} />
          </label>
          <label className={labelClass}>
            kWh/h
            <input type="number" step="0.001" className={inputClass} value={form.kwhPerHour} onChange={(e) => setForm((f) => ({ ...f, kwhPerHour: parseFloat(e.target.value) || 0 }))} />
          </label>
          <label className={labelClass}>
            Start fee / tisk (Kč)
            <input type="number" step="0.01" className={inputClass} value={form.startFeePerPrint} onChange={(e) => setForm((f) => ({ ...f, startFeePerPrint: parseFloat(e.target.value) || 0 }))} />
          </label>
          <label className={`${labelClass} sm:col-span-2`}>
            Objem (popis)
            <input className={inputClass} value={form.maxVolumeDescription} onChange={(e) => setForm((f) => ({ ...f, maxVolumeDescription: e.target.value }))} />
          </label>
          <label className={`${labelClass} sm:col-span-2`}>
            Poznámka
            <input className={inputClass} value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} />
          </label>
        </form>
      </Modal>
    </div>
  );
}
