"use client";

import { useCallback, useEffect, useState } from "react";
import { api, type Paged, type Product, type ProductInput } from "@/lib/api";
import { Modal, Pager, btnCls, btnGhostCls, inputCls } from "@/components/Modal";

const PAGE_SIZE = 20;
const money = new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB" });

export default function ProductsPage() {
  const [data, setData] = useState<Paged<Product> | null>(null);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [showInactive, setShowInactive] = useState(false);
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<Product | "new" | null>(null);

  const load = useCallback(async () => {
    try {
      setError(null);
      setData(await api.listProducts({ search: query, includeInactive: showInactive, page, pageSize: PAGE_SIZE }));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [query, showInactive, page]);

  useEffect(() => {
    load();
  }, [load]);

  async function deactivate(p: Product) {
    if (!confirm(`Deactivate ${p.sku} - ${p.name}?`)) return;
    try {
      await api.deactivateProduct(p.sku);
      await load();
    } catch (e) {
      setError((e as Error).message);
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
          <input className={inputCls} placeholder="Search name or SKU" value={search} onChange={(e) => setSearch(e.target.value)} />
          <button className={btnGhostCls}>Search</button>
        </form>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={showInactive}
            onChange={(e) => {
              setPage(1);
              setShowInactive(e.target.checked);
            }}
          />
          Show inactive
        </label>
        <button className={btnCls} onClick={() => setEditing("new")}>
          + New product
        </button>
      </div>

      {error && <p role="alert" className="mb-3 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{error}</p>}

      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
            <tr>
              <th className="px-3 py-2">SKU</th>
              <th className="px-3 py-2">Name</th>
              <th className="px-3 py-2">Category</th>
              <th className="px-3 py-2 text-right">Price</th>
              <th className="px-3 py-2 text-right">Available</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((p) => (
              <tr key={p.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2 font-mono">{p.sku}</td>
                <td className="px-3 py-2">{p.name}</td>
                <td className="px-3 py-2">{p.category ?? "-"}</td>
                <td className="px-3 py-2 text-right">{money.format(p.price)}</td>
                <td className="px-3 py-2 text-right">{p.available}</td>
                <td className="px-3 py-2">{p.isActive ? "Active" : <span className="text-zinc-500">Inactive</span>}</td>
                <td className="space-x-2 px-3 py-2 text-right whitespace-nowrap">
                  <button className="text-blue-600 hover:underline" onClick={() => setEditing(p)}>
                    Edit
                  </button>
                </td>
              </tr>
            ))}
            {data && data.items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-3 py-8 text-center text-zinc-500">
                  No products found.
                </td>
              </tr>
            )}
            {!data && !error && (
              <tr>
                <td colSpan={7} className="px-3 py-8 text-center text-zinc-500">
                  Loading...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && <Pager page={page} pageSize={PAGE_SIZE} total={data.total} onPage={setPage} />}

      {editing && (
        <ProductForm
          product={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={async () => {
            setEditing(null);
            await load();
          }}
        />
      )}
    </div>
  );
}

function ProductForm({
  product,
  onClose,
  onSaved,
}: {
  product: Product | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const text = (k: string) => (String(f.get(k) ?? "").trim() || null);
    const body: ProductInput = {
      name: String(f.get("name") ?? "").trim(),
      description: text("description"),
      price: Number(f.get("price")),
      category: text("category"),
      imageUrl: text("imageUrl"),
    };
    setBusy(true);
    setError(null);
    try {
      if (product) {
        await api.updateProduct(product.sku, { ...body, isActive: f.get("isActive") === "on" });
      } else {
        await api.createProduct({
          ...body,
          sku: String(f.get("sku") ?? "").trim(),
          initialQuantity: Number(f.get("initialQuantity") || 0),
        });
      }
      onSaved();
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  }

  return (
    <Modal title={product ? `Edit ${product.sku}` : "New product"} onClose={onClose}>
      <form onSubmit={submit} className="space-y-3">
        <Field label="SKU">
          <input name="sku"  className={inputCls} required maxLength={50} pattern="[A-Za-z0-9._\-]+" defaultValue={product?.sku} disabled={!!product} />
        </Field>
        <Field label="Name">
          <input name="name" className={inputCls} required maxLength={255} defaultValue={product?.name} />
        </Field>
        <Field label="Description">
          <textarea name="description" className={inputCls} rows={3} maxLength={2000} defaultValue={product?.description ?? ""} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Price (THB)">
            <input name="price" type="number" min="0" step="0.01" className={inputCls} required defaultValue={product?.price ?? ""} />
          </Field>
          <Field label="Category">
            <input name="category" className={inputCls} maxLength={100} defaultValue={product?.category ?? ""} />
          </Field>
        </div>
        <Field label="Image URL">
          <input name="imageUrl" className={inputCls} maxLength={500} defaultValue={product?.imageUrl ?? ""} />
        </Field>
        {product ? (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" name="isActive" defaultChecked={product.isActive} /> Active
          </label>
        ) : (
          <Field label="Initial quantity">
            <input name="initialQuantity" type="number" min="0" step="1" className={inputCls} defaultValue={0} />
          </Field>
        )}
        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className={btnGhostCls} onClick={onClose}>
            Cancel
          </button>
          <button className={btnCls} disabled={busy}>
            {busy ? "Saving..." : "Save"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block text-zinc-600 dark:text-zinc-400">{label}</span>
      {children}
    </label>
  );
}
