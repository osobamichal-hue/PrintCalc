"use client";

import { useCallback, useEffect, useState } from "react";
import { Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBanner } from "@/components/ui/StatusBanner";
import { apiJson, apiVoid } from "@/lib/api";
import { btnDanger, btnPrimary, btnSecondary, inputClass, labelClass, linkDanger, tableBody, tableHead, tableWrap } from "@/lib/ui";

type FilamentType = {
  id: number;
  name: string;
  manufacturer: string | null;
  diameterMm: number;
  color: string | null;
  densityGPerCm3: number;
  averagePricePerKg: number;
};

type Stock = {
  id: number;
  filamentTypeId: number;
  filamentTypeName: string;
  remainingWeightKg: number;
  pieceCount: number;
  receivedAt: string;
  lotNumber: string | null;
};

type Movement = {
  id: number;
  filamentTypeName: string;
  movementType: string;
  deltaKg: number;
  occurredAt: string;
  note: string | null;
};

type ModalKind = "type" | "receive" | "issue" | null;

export default function FilamentsPage() {
  const [types, setTypes] = useState<FilamentType[]>([]);
  const [stocks, setStocks] = useState<Stock[]>([]);
  const [movements, setMovements] = useState<Movement[]>([]);
  const [tab, setTab] = useState<"types" | "stock" | "movements">("types");
  const [msg, setMsg] = useState<string | null>(null);
  const [modal, setModal] = useState<ModalKind>(null);

  const [typeForm, setTypeForm] = useState({
    name: "PLA",
    manufacturer: "",
    diameterMm: 1.75,
    color: "",
    densityGPerCm3: 1.24,
  });

  const [recv, setRecv] = useState({
    filamentTypeId: 0,
    weightKg: 1,
    purchasePricePerKg: 400,
    supplier: "",
    pieceCount: 1,
  });

  const [issue, setIssue] = useState({
    filamentTypeId: 0,
    weightKg: 0.1,
    note: "",
  });

  const loadTypes = useCallback(async () => {
    const t = await apiJson<FilamentType[]>("/api/filament-types");
    setTypes(t);
    setRecv((r) => ({ ...r, filamentTypeId: r.filamentTypeId || t[0]?.id || 0 }));
    setIssue((i) => ({ ...i, filamentTypeId: i.filamentTypeId || t[0]?.id || 0 }));
  }, []);

  const loadStocks = useCallback(async () => {
    setStocks(await apiJson<Stock[]>("/api/filament-stocks?activeOnly=true"));
  }, []);

  const loadMovements = useCallback(async () => {
    setMovements(await apiJson<Movement[]>("/api/stock-movements"));
  }, []);

  const refresh = useCallback(async () => {
    setMsg(null);
    try {
      await loadTypes();
      await loadStocks();
      await loadMovements();
    } catch (e) {
      setMsg(e instanceof Error ? e.message : "Chyba");
    }
  }, [loadMovements, loadStocks, loadTypes]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  function closeModal() {
    setModal(null);
    setMsg(null);
  }

  async function addType(e: React.FormEvent) {
    e.preventDefault();
    setMsg(null);
    try {
      await apiJson("/api/filament-types", {
        method: "POST",
        body: JSON.stringify({
          name: typeForm.name,
          manufacturer: typeForm.manufacturer || null,
          diameterMm: typeForm.diameterMm,
          color: typeForm.color || null,
          densityGPerCm3: typeForm.densityGPerCm3,
          nozzleTempMinC: null,
          nozzleTempMaxC: null,
          bedTempMinC: null,
          bedTempMaxC: null,
          notes: null,
        }),
      });
      setTypeForm({ name: "PLA", manufacturer: "", diameterMm: 1.75, color: "", densityGPerCm3: 1.24 });
      closeModal();
      await refresh();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  async function delType(id: number) {
    if (!confirm("Smazat typ včetně skladových dat?")) return;
    try {
      await apiVoid(`/api/filament-types/${id}`, { method: "DELETE" });
      await refresh();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  async function receive(e: React.FormEvent) {
    e.preventDefault();
    setMsg(null);
    try {
      await apiVoid("/api/stock/receive", {
        method: "POST",
        body: JSON.stringify({
          filamentTypeId: recv.filamentTypeId,
          weightKg: recv.weightKg,
          purchasePricePerKg: recv.purchasePricePerKg,
          supplier: recv.supplier || null,
          pieceCount: recv.pieceCount,
          lotNumber: null,
          expirationDate: null,
          notes: null,
        }),
      });
      closeModal();
      await refresh();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  async function issueStock(e: React.FormEvent) {
    e.preventDefault();
    setMsg(null);
    try {
      await apiVoid("/api/stock/issue", {
        method: "POST",
        body: JSON.stringify({
          filamentTypeId: issue.filamentTypeId,
          weightKg: issue.weightKg,
          note: issue.note || "Výdej",
        }),
      });
      closeModal();
      await refresh();
    } catch (err) {
      setMsg(err instanceof Error ? err.message : "Chyba");
    }
  }

  const tabButtons = (
    <div className="flex flex-wrap gap-2">
      {([
        ["types", "Typy"],
        ["stock", "Sklad"],
        ["movements", "Pohyby"],
      ] as const).map(([k, label]) => (
        <button
          key={k}
          type="button"
          onClick={() => setTab(k)}
          className={`rounded-md px-3 py-1.5 text-sm ${tab === k ? "bg-amber-600 text-zinc-900 dark:text-zinc-950" : btnSecondary}`}
        >
          {label}
        </button>
      ))}
    </div>
  );

  return (
    <div className="space-y-4">
      <PageHeader title="Filamenty a sklad" description="Typy materiálu, skladové karty a pohyby.">
        {tab === "types" && (
          <button type="button" onClick={() => setModal("type")} className={btnPrimary}>
            + Nový typ
          </button>
        )}
        {tab === "stock" && (
          <>
            <button type="button" onClick={() => setModal("receive")} className={btnPrimary}>
              Příjem
            </button>
            <button type="button" onClick={() => setModal("issue")} className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-500">
              Výdej
            </button>
          </>
        )}
        <button type="button" onClick={() => void refresh()} className={btnSecondary}>
          Obnovit
        </button>
      </PageHeader>

      {tabButtons}
      {msg && !modal && <StatusBanner message={msg} />}

      {tab === "types" && (
        <div className={tableWrap}>
          <table className="w-full text-left text-sm">
            <thead className={tableHead}>
              <tr>
                <th className="px-3 py-2">Typ</th>
                <th className="px-3 py-2">Výrobce</th>
                <th className="px-3 py-2">Kč/kg</th>
                <th />
              </tr>
            </thead>
            <tbody className={tableBody}>
              {types.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-3 py-8 text-center text-zinc-500">
                    Žádné typy.{" "}
                    <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={() => setModal("type")}>
                      Přidat typ
                    </button>
                  </td>
                </tr>
              ) : (
                types.map((t) => (
                  <tr key={t.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                    <td className="px-3 py-2 font-medium text-zinc-800 dark:text-zinc-200">{t.name}</td>
                    <td className="px-3 py-2 text-zinc-500">{t.manufacturer ?? "—"}</td>
                    <td className="px-3 py-2 text-zinc-500">{t.averagePricePerKg.toFixed(2)}</td>
                    <td className="px-3 py-2">
                      <button type="button" className={linkDanger} onClick={() => void delType(t.id)}>Smazat</button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {tab === "stock" && (
        <div className={tableWrap}>
          <table className="w-full text-left text-sm">
            <thead className={tableHead}>
              <tr>
                <th className="px-3 py-2">Typ</th>
                <th className="px-3 py-2">Zbývá kg</th>
                <th className="px-3 py-2">Kusů</th>
                <th className="px-3 py-2">Přijato</th>
              </tr>
            </thead>
            <tbody className={tableBody}>
              {stocks.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-3 py-8 text-center text-zinc-500">
                    Sklad je prázdný.{" "}
                    <button type="button" className="text-amber-600 hover:underline dark:text-amber-400" onClick={() => setModal("receive")}>
                      Naskladnit
                    </button>
                  </td>
                </tr>
              ) : (
                stocks.map((s) => (
                  <tr key={s.id} className="hover:bg-zinc-50 dark:hover:bg-zinc-900/50">
                    <td className="px-3 py-2">{s.filamentTypeName}</td>
                    <td className="px-3 py-2">{s.remainingWeightKg}</td>
                    <td className="px-3 py-2">{s.pieceCount}</td>
                    <td className="px-3 py-2 text-zinc-500">{new Date(s.receivedAt).toLocaleString("cs-CZ")}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {tab === "movements" && (
        <div className={tableWrap}>
          <table className="w-full text-left text-sm">
            <thead className={tableHead}>
              <tr>
                <th className="px-3 py-2">Čas</th>
                <th className="px-3 py-2">Typ</th>
                <th className="px-3 py-2">Akce</th>
                <th className="px-3 py-2">Δ kg</th>
                <th className="px-3 py-2">Pozn.</th>
              </tr>
            </thead>
            <tbody className={tableBody}>
              {movements.map((m) => (
                <tr key={m.id}>
                  <td className="whitespace-nowrap px-3 py-2 text-zinc-500">{new Date(m.occurredAt).toLocaleString("cs-CZ")}</td>
                  <td className="px-3 py-2">{m.filamentTypeName}</td>
                  <td className="px-3 py-2">{m.movementType}</td>
                  <td className="px-3 py-2">{m.deltaKg}</td>
                  <td className="max-w-xs truncate px-3 py-2 text-zinc-500">{m.note ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        open={modal === "type"}
        onClose={closeModal}
        title="Nový typ filamentu"
        footer={
          <>
            <button type="button" onClick={closeModal} className={btnSecondary}>Zrušit</button>
            <button type="submit" form="type-form" className={btnPrimary}>Přidat</button>
          </>
        }
      >
        {msg && modal === "type" && <StatusBanner message={msg} />}
        <form id="type-form" onSubmit={(e) => void addType(e)} className="mt-3 space-y-3">
          <input placeholder="Název *" className={inputClass.replace("mt-1 ", "")} value={typeForm.name} onChange={(e) => setTypeForm((f) => ({ ...f, name: e.target.value }))} />
          <input placeholder="Výrobce" className={inputClass.replace("mt-1 ", "")} value={typeForm.manufacturer} onChange={(e) => setTypeForm((f) => ({ ...f, manufacturer: e.target.value }))} />
          <div className="grid grid-cols-2 gap-2">
            <input type="number" step="0.01" title="Průměr mm" placeholder="Průměr mm" className={inputClass.replace("mt-1 ", "")} value={typeForm.diameterMm} onChange={(e) => setTypeForm((f) => ({ ...f, diameterMm: parseFloat(e.target.value) || 1.75 }))} />
            <input placeholder="Barva" className={inputClass.replace("mt-1 ", "")} value={typeForm.color} onChange={(e) => setTypeForm((f) => ({ ...f, color: e.target.value }))} />
          </div>
        </form>
      </Modal>

      <Modal
        open={modal === "receive"}
        onClose={closeModal}
        title="Příjem na sklad"
        footer={
          <>
            <button type="button" onClick={closeModal} className={btnSecondary}>Zrušit</button>
            <button type="submit" form="receive-form" className={btnPrimary}>Naskladnit</button>
          </>
        }
      >
        {msg && modal === "receive" && <StatusBanner message={msg} />}
        <form id="receive-form" onSubmit={(e) => void receive(e)} className="mt-3 space-y-3">
          <label className={labelClass}>
            Typ
            <select className={inputClass} value={recv.filamentTypeId} onChange={(e) => setRecv((r) => ({ ...r, filamentTypeId: parseInt(e.target.value, 10) }))}>
              {types.map((t) => (<option key={t.id} value={t.id}>{t.name}</option>))}
            </select>
          </label>
          <div className="grid grid-cols-2 gap-2">
            <label className={labelClass}>kg<input type="number" step="0.001" className={inputClass} value={recv.weightKg} onChange={(e) => setRecv((r) => ({ ...r, weightKg: parseFloat(e.target.value) || 0 }))} /></label>
            <label className={labelClass}>Kč/kg<input type="number" step="0.01" className={inputClass} value={recv.purchasePricePerKg} onChange={(e) => setRecv((r) => ({ ...r, purchasePricePerKg: parseFloat(e.target.value) || 0 }))} /></label>
          </div>
          <input placeholder="Dodavatel" className={inputClass.replace("mt-1 ", "")} value={recv.supplier} onChange={(e) => setRecv((r) => ({ ...r, supplier: e.target.value }))} />
          <label className={labelClass}>Počet kusů<input type="number" min={1} className={inputClass} value={recv.pieceCount} onChange={(e) => setRecv((r) => ({ ...r, pieceCount: parseInt(e.target.value, 10) || 1 }))} /></label>
        </form>
      </Modal>

      <Modal
        open={modal === "issue"}
        onClose={closeModal}
        title="Výdej ze skladu"
        footer={
          <>
            <button type="button" onClick={closeModal} className={btnSecondary}>Zrušit</button>
            <button type="submit" form="issue-form" className={btnDanger}>Vyskladnit</button>
          </>
        }
      >
        {msg && modal === "issue" && <StatusBanner message={msg} />}
        <form id="issue-form" onSubmit={(e) => void issueStock(e)} className="mt-3 space-y-3">
          <label className={labelClass}>
            Typ
            <select className={inputClass} value={issue.filamentTypeId} onChange={(e) => setIssue((i) => ({ ...i, filamentTypeId: parseInt(e.target.value, 10) }))}>
              {types.map((t) => (<option key={t.id} value={t.id}>{t.name}</option>))}
            </select>
          </label>
          <label className={labelClass}>kg<input type="number" step="0.001" className={inputClass} value={issue.weightKg} onChange={(e) => setIssue((i) => ({ ...i, weightKg: parseFloat(e.target.value) || 0 }))} /></label>
          <input placeholder="Poznámka" className={inputClass.replace("mt-1 ", "")} value={issue.note} onChange={(e) => setIssue((i) => ({ ...i, note: e.target.value }))} />
        </form>
      </Modal>
    </div>
  );
}
