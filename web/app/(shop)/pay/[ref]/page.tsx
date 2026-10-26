"use client";

import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api, type MockPayment } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import { btnCls, btnGhostCls } from "@/components/Modal";

/** Stand-in for a provider's hosted payment page. The outcome is delivered to the API as a webhook, not trusted from here. */
export default function MockPayPage() {
  const { ref } = useParams<{ ref: string }>();
  const router = useRouter();
  const [payment, setPayment] = useState<MockPayment | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api
      .getMockPayment(ref)
      .then(setPayment)
      .catch((e) => setError((e as Error).message));
  }, [ref]);

  async function simulate(outcome: "succeeded" | "failed") {
    if (!payment || busy) return;
    setBusy(true);
    setError(null);
    try {
      const result = await api.simulateMockPayment(ref, outcome);
      router.push(`/orders/${result.orderId}`);
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  }

  if (error && !payment) return <p role="alert" className="text-red-600">{error}</p>;
  if (!payment) return <p className="text-sm text-zinc-500">Loading...</p>;

  const open = payment.status === "Pending";

  return (
    <div className="max-w-md rounded border border-zinc-200 p-6 dark:border-zinc-800">
      <p className="mb-1 text-xs tracking-wide text-zinc-500 uppercase">Mock payment provider (development only)</p>
      <h1 className="mb-1 text-xl font-semibold">Pay {formatMoney(payment.amount)}</h1>
      <p className="mb-4 font-mono text-xs break-all text-zinc-500">{payment.providerRef}</p>

      {!open && <p className="mb-4 text-sm">This payment is already {payment.status}.</p>}
      {error && <p role="alert" className="mb-3 text-sm text-red-600">{error}</p>}

      <div className="flex gap-3">
        <button className={btnCls} disabled={!open || busy} onClick={() => simulate("succeeded")}>
          Pay successfully
        </button>
        <button className={btnGhostCls} disabled={!open || busy} onClick={() => simulate("failed")}>
          Simulate failure
        </button>
      </div>
      <button className="mt-4 text-sm text-blue-600 hover:underline" onClick={() => router.push(`/orders/${payment.orderId}`)}>
        Back to order
      </button>
    </div>
  );
}
