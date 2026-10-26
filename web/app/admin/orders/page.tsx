"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, type OrderStatus, type OrderSummary, type Paged } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import { Pager, inputCls } from "@/components/Modal";

const PAGE_SIZE = 20;
const STATUSES: OrderStatus[] = ["Pending", "Paid", "Cancelled", "Expired"];

export default function AdminOrdersPage() {
  const [data, setData] = useState<Paged<OrderSummary> | null>(null);
  const [status, setStatus] = useState<OrderStatus | "">("");
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setError(null);
      setData(await api.listOrders({ status: status || undefined, page, pageSize: PAGE_SIZE }));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [status, page]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <h1 className="mr-auto text-xl font-semibold">Orders</h1>
        <select
          aria-label="Filter by status"
          className={`${inputCls} w-40`}
          value={status}
          onChange={(e) => {
            setPage(1);
            setStatus(e.target.value as OrderStatus | "");
          }}
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </div>

      {error && <p role="alert" className="mb-3 rounded bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300">{error}</p>}

      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
            <tr>
              <th className="px-3 py-2">Created</th>
              <th className="px-3 py-2">Customer</th>
              <th className="px-3 py-2 text-right">Items</th>
              <th className="px-3 py-2 text-right">Total</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((o) => (
              <tr key={o.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2 whitespace-nowrap">{new Date(o.createdAt).toLocaleString()}</td>
                <td className="px-3 py-2">
                  {o.customerName}
                  <div className="text-xs text-zinc-500">{o.customerEmail}</div>
                </td>
                <td className="px-3 py-2 text-right">{o.itemCount}</td>
                <td className="px-3 py-2 text-right">{formatMoney(o.total)}</td>
                <td className="px-3 py-2">{o.status}</td>
                <td className="px-3 py-2 text-right">
                  <Link href={`/orders/${o.id}`} className="text-blue-600 hover:underline">
                    View
                  </Link>
                </td>
              </tr>
            ))}
            {data && data.items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-zinc-500">
                  No orders.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && <Pager page={page} pageSize={PAGE_SIZE} total={data.total} onPage={setPage} />}
    </div>
  );
}
