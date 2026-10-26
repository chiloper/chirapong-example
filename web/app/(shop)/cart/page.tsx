"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type Cart } from "@/lib/api";
import { formatMoney, notifyCartChanged } from "@/lib/format";
import { btnCls, btnGhostCls, inputCls } from "@/components/Modal";

export default function CartPage() {
  const [cart, setCart] = useState<Cart | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Bumped after a failed update so uncontrolled quantity inputs snap back to the server value.
  const [resetKey, setResetKey] = useState(0);

  const load = useCallback(async () => {
    try {
      setCart(await api.getCart());
    } catch (e) {
      setError((e as Error).message);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function run(action: () => Promise<Cart>) {
    setError(null);
    try {
      setCart(await action());
      notifyCartChanged();
    } catch (e) {
      setError((e as Error).message);
      setResetKey((k) => k + 1);
      load();
    }
  }

  if (!cart && !error) return <p className="text-sm text-zinc-500">Loading...</p>;

  const blocked = cart?.items.some((i) => !i.isAvailable) ?? false;
  const ProductBlock = cart?.items.map((m) => {
    if (m.isAvailable === false)
      return m
  })

  return (
    <div>
      <h1 className="mb-4 text-xl font-semibold">Your cart</h1>
      {error && <p role="alert" className="mb-3 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{error}</p>}

      {cart && cart.items.length === 0 && (
        <p className="py-8 text-center text-zinc-500">
          Your cart is empty.{" "}
          <Link href="/" className="text-blue-600 hover:underline">
            Continue shopping
          </Link>
        </p>
      )}

      {cart && cart.items.length > 0 && (
        <>
          <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
            <table className="w-full text-left text-sm">
              <thead className="bg-zinc-50 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
                <tr>
                  <th className="px-3 py-2">Product</th>
                  <th className="px-3 py-2 text-right">Price</th>
                  <th className="px-3 py-2">Qty</th>
                  <th className="px-3 py-2 text-right">Total</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {cart.items.map((i) => (
                  <tr key={i.sku} className="border-t border-zinc-200 dark:border-zinc-800">
                    <td className="px-3 py-2">
                      {i.name}
                      <div className="font-mono text-xs text-zinc-500">{i.sku}</div>
                      {i.available <= 0 && (
                        <div className="text-xs text-red-600">
                          {"Out of stock - remove this item."}
                        </div>
                      )}
                    </td>
                    <td className="px-3 py-2 text-right">{formatMoney(i.unitPrice)}</td>
                    <td className="px-3 py-2">
                      <input
                        type="number"
                        min={1}
                        max={999}
                        defaultValue={i.quantity}
                        key={`${i.sku}-${i.quantity}-${resetKey}`}
                        aria-label={`Quantity for ${i.name}`}
                        className={`${inputCls} w-20`}
                        onBlur={(e) => {
                          const q = Number(e.target.value);
                          if (Number.isInteger(q) && q >= 1 && q !== i.quantity) run(() => api.setCartQuantity(i.sku, q));
                          else e.target.value = String(i.quantity);
                        }}
                      />
                    </td>
                    <td className="px-3 py-2 text-right">{formatMoney(i.lineTotal)}</td>
                    <td className="px-3 py-2 text-right">
                      <button className="text-red-600 hover:underline" onClick={() => run(() => api.removeFromCart(i.sku))}>
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between">
            <Link href="/" className={btnGhostCls}>
              Continue shopping
            </Link>
            <div className="flex items-center gap-4">
              <p className="text-lg font-semibold">Total {formatMoney(cart.total)}</p>
              {blocked ? (
                <button className={btnCls} disabled>
                  Checkout
                </button>
              ) : (
                <Link href="/checkout" className={btnCls}>
                  Checkout
                </Link>
              )}
            </div>
          </div>
          {blocked && (
            <div className="text-xs text-red-600  text-end mt-1">
              {ProductBlock?.map((b) => b?.name).join(' And ')} is no Available
            </div>
          )}
        </>
      )}
    </div>
  );
}
