import Link from "next/link";
import { CartLink } from "@/components/CartLink";

export default function ShopLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen">
      <header className="border-b border-zinc-200 dark:border-zinc-800">
        <nav className="mx-auto flex max-w-5xl items-center gap-6 px-4 py-3 text-sm">
          <Link href="/" className="font-semibold">
            Shop
          </Link>
          <span className="ml-auto" />
          <CartLink />
          <Link href="/admin/products" className="text-zinc-500 hover:underline">
            Admin
          </Link>
        </nav>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-6">{children}</main>
    </div>
  );
}
