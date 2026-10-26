"use client";

import { useCallback, useEffect, useState } from "react";
import { api, type Paged, type Product } from "@/lib/api";
import { formatMoney, notifyCartChanged } from "@/lib/format";
import { Pager, btnCls, btnGhostCls, inputCls } from "@/components/Modal";

const PAGE_SIZE = 12;

export default function ShopPage() {
  const [data, setData] = useState<Paged<Product> | null>(null);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busySku, setBusySku] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setError(null);
      setData(await api.listProducts({ search: query, page, pageSize: PAGE_SIZE }));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [query, page]);

  useEffect(() => {
    load();
  }, [load]);

  async function add(p: Product) {
    setBusySku(p.sku);
    setNotice(null);
    setError(null);
    try {
      await api.addToCart(p.sku, 1);
      notifyCartChanged();
      setNotice(`Added ${p.name} to your cart.`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusySku(null);
      load();
    }
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <h1 className="mr-auto text-xl font-semibold">Products</h1>
        <form
          className="flex gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            setPage(1);
            setQuery(search);
          }}
        >
          <input className={inputCls} placeholder="Search" value={search} onChange={(e) => setSearch(e.target.value)} />
          <button className={btnGhostCls}>Search</button>
        </form>
      </div>

      {notice && <p role="status" className="mb-3 rounded bg-green-50 px-3 py-2 text-sm text-green-800 dark:bg-green-950 dark:text-green-300">{notice}</p>}
      {error && <p role="alert" className="mb-3 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{error}</p>}

      {!data && !error && <p className="text-sm text-zinc-500">Loading...</p>}
      {data && data.items.length === 0 && <p className="py-8 text-center text-zinc-500">No products found.</p>}

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {data?.items.map((p) => (
          <li key={p.id} className="flex flex-col rounded border border-zinc-200 p-4 dark:border-zinc-800">
            <h2 className="font-medium">{p.name}</h2>
            <p className="font-mono text-xs text-zinc-500">{p.sku}</p>
            {p.description && <p className="mt-2 line-clamp-3 text-sm text-zinc-600 dark:text-zinc-400">{p.description}</p>}
            <div className="mt-auto flex items-end justify-between pt-4">
              <div>
                <p className="text-lg font-semibold">{formatMoney(p.price)}</p>
                <p className={`text-xs ${p.available > 0 ? "text-zinc-500" : "text-red-600"}`}>
                  {p.available > 0 ? `${p.available} in stock` : "Out of stock"}
                </p>
              </div>
              <button className={btnCls} disabled={p.available <= 0 || busySku === p.sku} onClick={() => add(p)}>
                {busySku === p.sku ? "Adding..." : "Add to cart"}
              </button>
            </div>
          </li>
        ))}
      </ul>

      {data && <Pager page={page} pageSize={PAGE_SIZE} total={data.total} onPage={setPage} />}
    </div>
  );
}
