"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { api, type Order } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import { btnCls, btnGhostCls } from "@/components/Modal";

function useCountdown(target: string | null) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!target) return;
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, [target]);
  return target ? Math.max(0, Math.floor((new Date(target).getTime() - now) / 1000)) : 0;
}

export default function OrderPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [order, setOrder] = useState<Order | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setOrder(await api.getOrder(id));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const pending = order?.status === "Pending";
  const secondsLeft = useCountdown(pending ? order!.expiresAt : null);

  // Once the reservation window closes, refetch so the page shows the server's verdict (Expired).
  useEffect(() => {
    if (pending && secondsLeft === 0) {
      const t = setTimeout(load, 4000);
      return () => clearTimeout(t);
    }
  }, [pending, secondsLeft, load]);

  async function pay() {
    setBusy(true);
    setError(null);
    try {
      const started = await api.startPayment(id);
      router.push(started.paymentUrl);
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
      load();
    }
  }

  async function cancel() {
    if (!confirm("Cancel this order and release the reserved stock?")) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.cancelOrder(id));
    } catch (e) {
      setError((e as Error).message);
      load();
    } finally {
      setBusy(false);
    }
  }

  if (error && !order) return <p role="alert" className="text-red-600">{error}</p>;
  if (!order) return <p className="text-sm text-zinc-500">Loading...</p>;

  const mm = String(Math.floor(secondsLeft / 60)).padStart(2, "0");
  const ss = String(secondsLeft % 60).padStart(2, "0");

  return (
    <div className="max-w-2xl">
      <h1 className="mb-1 text-xl font-semibold">Order received</h1>
      <p className="mb-4 font-mono text-xs break-all text-zinc-500">{order.id}</p>

      <p className="mb-4 text-sm">
        Status: <strong>{order.status}</strong>
        {pending && (
          <span className="ml-3 text-zinc-600 dark:text-zinc-400">
            {secondsLeft > 0 ? `Stock reserved for ${mm}:${ss}` : "Reservation ending..."}
          </span>
        )}
      </p>
      {error && <p role="alert" className="mb-3 text-sm text-red-600">{error}</p>}
      {order.status === "Paid" && <p className="mb-3 text-sm text-green-700 dark:text-green-400">Payment received{order.paidAt ? ` at ${new Date(order.paidAt).toLocaleString()}` : ""}. Thank you!</p>}
      {order.status === "Expired" &&<p className="mb-3 text-sm text-zinc-600 dark:text-zinc-400">The reservation expired and the items were returned to stock.</p>}

      <ul className="divide-y divide-zinc-200 rounded border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
        {order.items.map((i) => (
          <li key={i.sku} className="flex justify-between px-3 py-2">
            <span>
              {i.name} <span className="text-zinc-500">x {i.quantity}</span>
            </span>
            <span>{formatMoney(i.lineTotal)}</span>
          </li>
        ))}
        <li className="flex justify-between px-3 py-2 font-semibold">
          <span>Total</span>
          <span>{formatMoney(order.total)}</span>
        </li>
      </ul>

      <p className="mt-4 text-sm text-zinc-600 dark:text-zinc-400">
        {order.customerName} - {order.customerEmail}
        {order.customerPhone ? ` - ${order.customerPhone}` : ""}
      </p>
      {order.shippingAddress && <p className="text-sm text-zinc-600 dark:text-zinc-400">{order.shippingAddress}</p>}

      <div className="mt-6 flex gap-3">
        {pending && (
          <button className={btnCls} onClick={pay} disabled={busy || secondsLeft === 0}>
            Pay now
          </button>
        )}
        {pending && (
          <button className={btnGhostCls} onClick={cancel} disabled={busy}>
            Cancel order
          </button>
        )}
        <Link href="/" className={btnGhostCls}>
          Back to shop
        </Link>
      </div>
      <p className="mt-4 text-xs text-zinc-500">Keep this page&apos;s link: it is the only way to find your order.</p>
    </div>
  );
}
