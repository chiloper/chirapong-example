import Link from "next/link";

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen">
      <header className="border-b border-zinc-200 dark:border-zinc-800">
        <nav className="mx-auto flex max-w-6xl items-center gap-6 px-4 py-3 text-sm">
          <span className="font-semibold">Stock Admin</span>
          <Link href="/admin/products" className="hover:underline">
            Products
          </Link>
          <Link href="/admin/stock" className="hover:underline">
            Stock
          </Link>
          <Link href="/admin/orders" className="hover:underline">
            Orders
          </Link>
          <Link href="/" className="ml-auto text-zinc-500 hover:underline">
            View shop
          </Link>
        </nav>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
    </div>
  );
}
