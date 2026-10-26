"use client";

import { useCallback, useEffect, useState } from "react";
import { api, type MovementReason, type Paged, type StockMovement, type StockRow } from "@/lib/api";
import { Modal, Pager, btnCls, btnGhostCls, inputCls } from "@/components/Modal";

const PAGE_SIZE = 20;
const LOW_STOCK = 5;

export default function StockPage() {
  const [data, setData] = useState<Paged<StockRow> | null>(null);
  const [lowOnly, setLowOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [adjusting, setAdjusting] = useState<StockRow | null>(null);
  const [history, setHistory] = useState<StockRow | null>(null);

  const load = useCallback(async () => {
    try {
      setError(null);
      setData(await api.listStocks({ lowStockOnly: lowOnly, lowStockThreshold: LOW_STOCK, page, pageSize: PAGE_SIZE }));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [lowOnly, page]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <h1 className="mr-auto text-xl font-semibold">Stock</h1>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={lowOnly}
            onChange={(e) => {
              setPage(1);
              setLowOnly(e.target.checked);
            }}
          />
          Low stock only (available ≤ {LOW_STOCK})
        </label>
      </div>

      {error && <p role="alert" className="mb-3 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{error}</p>}

      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
            <tr>
              <th className="px-3 py-2">SKU</th>
              <th className="px-3 py-2">Product</th>
              <th className="px-3 py-2 text-right">On hand</th>
              <th className="px-3 py-2 text-right">Reserved</th>
              <th className="px-3 py-2 text-right">Available</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((s) => (
              <tr key={s.sku} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2 font-mono">{s.sku}</td>
                <td className="px-3 py-2">{s.productName}</td>
                <td className="px-3 py-2 text-right">{s.quantityOnHand}</td>
                <td className="px-3 py-2 text-right">{s.quantityReserved}</td>
                <td className={`px-3 py-2 text-right font-medium ${s.available <= LOW_STOCK ? "text-red-600" : ""}`}>{s.available}</td>
                <td className="space-x-2 px-3 py-2 text-right whitespace-nowrap">
                  <button className="text-blue-600 hover:underline" onClick={() => setAdjusting(s)}>
                    Adjust
                  </button>
                  <button className="text-blue-600 hover:underline" onClick={() => setHistory(s)}>
                    History
                  </button>
                </td>
              </tr>
            ))}
            {data && data.items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-zinc-500">
                  No stock rows.
                </td>
              </tr>
            )}
            {!data && !error && (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-zinc-500">
                  Loading...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && <Pager page={page} pageSize={PAGE_SIZE} total={data.total} onPage={setPage} />}

      {adjusting && (
        <AdjustForm
          stock={adjusting}
          onClose={() => setAdjusting(null)}
          onSaved={async () => {
            setAdjusting(null);
            await load();
          }}
        />
      )}
      {history && <HistoryModal stock={history} onClose={() => setHistory(null)} />}
    </div>
  );
}

function AdjustForm({ stock, onClose, onSaved }: { stock: StockRow; onClose: () => void; onSaved: () => void }) {
  const [reason, setReason] = useState<MovementReason>("In");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const qty = Number(f.get("quantity"));
    // In = +qty, Out = -qty, Adjust = signed value as typed.
    const change = reason === "In" ? Math.abs(qty) : reason === "Out" ? -Math.abs(qty) : qty;
    setBusy(true);
    setError(null);
    try {
      await api.adjustStock(stock.sku, { change, reason, note: String(f.get("note") ?? "").trim() || null });
      onSaved();
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  }

  return (
    <Modal title={`Adjust ${stock.sku}`} onClose={onClose}>
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-400">
        {stock.productName} - on hand {stock.quantityOnHand}, reserved {stock.quantityReserved}
      </p>
      <form onSubmit={submit} className="space-y-3">
        <label className="block text-sm">
          <span className="mb-1 block text-zinc-600 dark:text-zinc-400">Type</span>
          <select className={inputCls} value={reason} onChange={(e) => setReason(e.target.value as MovementReason)}>
            <option value="In">Receive stock (+)</option>
            <option value="Out">Remove stock (-)</option>
            <option value="Adjust">Correction (+/-)</option>
          </select>
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-zinc-600 dark:text-zinc-400">
            Quantity{reason === "Adjust" ? " (negative to decrease)" : ""}
          </span>
          <input
            name="quantity"
            type="number"
            step="1"
            required
            min={reason === "Adjust" ? undefined : 1}
            className={inputCls}
          />
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-zinc-600 dark:text-zinc-400">Note</span>
          <input name="note" maxLength={500} className={inputCls} />
        </label>
        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className={btnGhostCls} onClick={onClose}>
            Cancel
          </button>
          <button className={btnCls} disabled={busy}>
            {busy ? "Saving..." : "Apply"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function HistoryModal({ stock, onClose }: { stock: StockRow; onClose: () => void }) {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Paged<StockMovement> | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .listMovements(stock.sku, page)
      .then(setData)
      .catch((e) => setError((e as Error).message));
  }, [stock.sku, page]);

  return (
    <Modal title={`History - ${stock.sku}`} onClose={onClose}>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      {!data && !error && <p className="text-sm text-zinc-500">Loading...</p>}
      {data && (
        <>
          <table className="w-full text-left text-sm">
            <thead className="text-zinc-600 dark:text-zinc-400">
              <tr>
                <th className="py-1">When</th>
                <th className="py-1">Type</th>
                <th className="py-1 text-right">Change</th>
                <th className="py-1 pl-3">Note</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((m) => (
                <tr key={m.id} className="border-t border-zinc-200 dark:border-zinc-800">
                  <td className="py-1 whitespace-nowrap">{new Date(m.createdAt).toLocaleString()}</td>
                  <td className="py-1">{m.reason}</td>
                  <td className={`py-1 text-right ${m.change < 0 ? "text-red-600" : "text-green-600"}`}>
                    {m.change > 0 ? `+${m.change}` : m.change}
                  </td>
                  <td className="py-1 pl-3">{m.note ?? ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pager page={page} pageSize={PAGE_SIZE} total={data.total} onPage={setPage} />
        </>
      )}
    </Modal>
  );
}
