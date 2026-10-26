"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";

export function CartLink() {
  const [count, setCount] = useState<number | null>(null);

  useEffect(() => {
    const load = () =>
      api
        .getCart()
        .then((c) => setCount(c.itemCount))
        .catch(() => setCount(null));
    load();
    window.addEventListener("cart:changed", load);
    return () => window.removeEventListener("cart:changed", load);
  }, []);

  return (
    <Link href="/cart" className="hover:underline">
      Cart{count ? ` (${count})` : ""}
    </Link>
  );
}
