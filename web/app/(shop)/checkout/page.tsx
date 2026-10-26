"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api, type Cart } from "@/lib/api";
import { formatMoney, notifyCartChanged } from "@/lib/format";
import { btnCls, inputCls } from "@/components/Modal";

export default function CheckoutPage() {
  const router = useRouter();
  const [cart, setCart] = useState<Cart | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.getCart().then(setCart).catch((e) => setError((e as Error).message));
  }, []);

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (busy) return;
    const f = new FormData(e.currentTarget);
    const text = (k: string) => String(f.get(k) ?? "").trim() || null;
    setBusy(true);
    setError(null);
    try {
      const order = await api.checkout({
        customerName: String(f.get("customerName") ?? "").trim(),
        customerEmail: String(f.get("customerEmail") ?? "").trim(),
        customerPhone: text("customerPhone"),
        shippingAddress: text("shippingAddress"),
      });
      notifyCartChanged();
      router.push(`/orders/${order.id}`);
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
      api.getCart().then(setCart).catch(() => undefined);
    }
  }

  if (!cart && !error) return <p className="text-sm text-zinc-500">Loading...</p>;

  if (cart && cart.items.length === 0) {
    return (
      <p className="py-8 text-center text-zinc-500">
        Your cart is empty.{" "}
        <Link href="/" className="text-blue-600 hover:underline">
          Continue shopping
        </Link>
      </p>
    );
  }

  return (
    <div className="grid gap-8 md:grid-cols-2">
      <form onSubmit={submit} className="space-y-3">
        <h1 className="text-xl font-semibold">Checkout</h1>
        <Field label="Full name">
          <input name="customerName" className={inputCls} required maxLength={200} autoComplete="name" />
        </Field>
        <Field label="Email">
          <input name="customerEmail" type="email" className={inputCls} required maxLength={255} autoComplete="email" />
        </Field>
        <Field label="Phone (optional)">
          <input name="customerPhone" className={inputCls} maxLength={50} autoComplete="tel" />
        </Field>
        <Field label="Shipping address (optional)">
          <textarea name="shippingAddress" className={inputCls} rows={3} maxLength={1000} autoComplete="street-address" />
        </Field>
        {error && <p role="alert" className="rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{error}</p>}
        <button className={btnCls} disabled={busy}>
          {busy ? "Placing order..." : "Place order & reserve stock"}
        </button>
        <p className="text-xs text-zinc-500">Your stock is reserved for a limited time. You pay on the next page.</p>
      </form>

      <section>
        <h2 className="mb-3 text-lg font-semibold">Order summary</h2>
        <ul className="divide-y divide-zinc-200 rounded border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
          {cart?.items.map((i) => (
            <li key={i.sku} className="flex justify-between px-3 py-2">
              <span>
                {i.name} <span className="text-zinc-500">x {i.quantity}</span>
              </span>
              <span>{formatMoney(i.lineTotal)}</span>
            </li>
          ))}
          <li className="flex justify-between px-3 py-2 font-semibold">
            <span>Total</span>
            <span>{formatMoney(cart?.total ?? 0)}</span>
          </li>
        </ul>
      </section>
    </div>
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
