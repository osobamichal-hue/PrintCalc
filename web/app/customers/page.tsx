"use client";

import { useCallback, useEffect, useState } from "react";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiVoid } from "@/lib/api";
import { btnPrimary, btnSecondary, inputClass, labelClass, linkAction, linkDanger, tableBody, tableHead, tableWrap } from "@/lib/ui";
import type { Customer, CustomerWrite } from "@/lib/types";

const emptyForm = (): CustomerWrite => ({
  name: "",
  companyId: "",
  vatId: "",
  street: "",
  city: "",
  zip: "",
  email: "",
  phone: "",
  invoiceDueDays: 14,
  preferredPaymentMethod: "Převodem",
});

export default function CustomersPage() {
  const [items, setItems] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState<CustomerWrite>(emptyForm());

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setItems(await apiJson<Customer[]>("/api/customers"));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Chyba načtení");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function openNew() {
    setEditId(null);
    setForm(emptyForm());
    setStatus(null);
    setFormOpen(true);
  }

  function openEdit(c: Customer) {
    setEditId(c.id);
    setForm({
      name: c.name,
      companyId: c.companyId ?? "",
      vatId: c.vatId ?? "",
      street: c.street ?? "",
      city: c.city ?? "",
      zip: c.zip ?? "",
      email: c.email ?? "",
      phone: c.phone ?? "",
      invoiceDueDays: c.invoiceDueDays ?? 14,
      preferredPaymentMethod: c.preferredPaymentMethod ?? "Převodem",
    });
    setStatus(null);
    setFormOpen(true);
  }

  function closeForm() {
    setFormOpen(false);
    setStatus(null);
  }

  async function onSave(e: React.FormEvent) {
    e.preventDefault();
    setStatus(null);
    if (!form.name?.trim()) {
      setStatus("Vyplňte alespoň název zákazníka.");
      return;
    }
    const body = {
      name: form.name.trim(),
      companyId: form.companyId || null,
      vatId: form.vatId || null,
      street: form.street || null,
      city: form.city || null,
      zip: form.zip || null,
      email: form.email || null,
      phone: form.phone || null,
      invoiceDueDays: form.invoiceDueDays ?? null,
      preferredPaymentMethod: form.preferredPaymentMethod || null,
    };
    try {
      if (editId) {
        await apiVoid(`/api/customers/${editId}`, {
          method: "PUT",
          body: JSON.stringify(body),
        });
      } else {
        await apiJson<Customer>("/api/customers", {
          method: "POST",
          body: JSON.stringify(body),
        });
      }
      closeForm();
      await load();
    } catch (err) {
      setStatus(err instanceof Error ? err.message : "Chyba při ukládání");
    }
  }

  async function onDelete(id: number) {
    if (!confirm("Opravdu smazat tohoto zákazníka?")) return;
    try {
      await apiVoid(`/api/customers/${id}`, { method: "DELETE" });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Chyba při mazání");
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Zákazníci" description="Evidence zákazníků a fakturačních údajů.">
        <button type="button" onClick={openNew} className={btnPrimary}>
          + Nový zákazník
        </button>
      </PageHeader>

      {error && !formOpen && <StatusBanner message={error} variant="error" />}

      <div className={tableWrap}>
        <table className="w-full min-w-[640px] text-left text-sm">
          <thead className={tableHead}>
            <tr>
              <th className="px-3 py-2">Název</th>
              <th className="px-3 py-2">IČ</th>
              <th className="px-3 py-2">Město</th>
              <th className="px-3 py-2">E-mail</th>
              <th className="px-3 py-2 w-28" />
            </tr>
          </thead>
          <tbody className={tableBody}>
            {loading ? (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-zinc-500">
                  Načítám…
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-zinc-500">
                  Zatím žádní zákazníci.{" "}
                  <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={openNew}>
                    Přidat prvního
                  </button>
                </td>
              </tr>
            ) : (
              items.map((c) => (
                <tr key={c.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                  <td className="px-3 py-2 font-medium text-zinc-800 dark:text-zinc-200">{c.name}</td>
                  <td className="px-3 py-2 text-zinc-500">{c.companyId ?? "—"}</td>
                  <td className="px-3 py-2 text-zinc-500">{c.city ?? "—"}</td>
                  <td className="px-3 py-2 text-zinc-500">{c.email ?? "—"}</td>
                  <td className="space-x-2 px-3 py-2 whitespace-nowrap">
                    <button type="button" className={linkAction} onClick={() => openEdit(c)}>
                      Upravit
                    </button>
                    <button type="button" className={linkDanger} onClick={() => void onDelete(c.id)}>
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
        title={editId ? `Upravit zákazníka` : "Nový zákazník"}
        size="xl"
        footer={
          <>
            <button type="button" onClick={closeForm} className={btnSecondary}>
              Zrušit
            </button>
            <button type="submit" form="customer-form" className={btnPrimary}>
              Uložit
            </button>
          </>
        }
      >
        {status && <StatusBanner message={status} />}
        <form id="customer-form" onSubmit={(e) => void onSave(e)} className="mt-3 grid gap-3 sm:grid-cols-2">
          <label className={labelClass}>
            Název *
            <input className={inputClass} value={form.name ?? ""} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
          </label>
          <label className={labelClass}>
            IČ
            <input className={inputClass} value={form.companyId ?? ""} onChange={(e) => setForm((f) => ({ ...f, companyId: e.target.value }))} />
          </label>
          <label className={labelClass}>
            DIČ
            <input className={inputClass} value={form.vatId ?? ""} onChange={(e) => setForm((f) => ({ ...f, vatId: e.target.value }))} />
          </label>
          <label className={labelClass}>
            Ulice
            <input className={inputClass} value={form.street ?? ""} onChange={(e) => setForm((f) => ({ ...f, street: e.target.value }))} />
          </label>
          <label className={labelClass}>
            Město
            <input className={inputClass} value={form.city ?? ""} onChange={(e) => setForm((f) => ({ ...f, city: e.target.value }))} />
          </label>
          <label className={labelClass}>
            PSČ
            <input className={inputClass} value={form.zip ?? ""} onChange={(e) => setForm((f) => ({ ...f, zip: e.target.value }))} />
          </label>
          <label className={labelClass}>
            E-mail
            <input className={inputClass} value={form.email ?? ""} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} />
          </label>
          <label className={labelClass}>
            Telefon
            <input className={inputClass} value={form.phone ?? ""} onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))} />
          </label>
          <label className={labelClass}>
            Splatnost faktur (dny)
            <input
              type="number"
              min={1}
              className={inputClass}
              value={form.invoiceDueDays ?? ""}
              onChange={(e) =>
                setForm((f) => ({
                  ...f,
                  invoiceDueDays: e.target.value ? parseInt(e.target.value, 10) : null,
                }))
              }
            />
          </label>
          <label className={labelClass}>
            Platební metoda
            <input
              className={inputClass}
              value={form.preferredPaymentMethod ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, preferredPaymentMethod: e.target.value }))}
            />
          </label>
        </form>
      </Modal>
    </div>
  );
}
