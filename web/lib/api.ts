export type Product = {
  id: number;
  sku: string;
  name: string;
  description: string | null;
  price: number;
  category: string | null;
  imageUrl: string | null;
  isActive: boolean;
  quantityOnHand: number;
  quantityReserved: number;
  available: number;
  createdAt: string;
  updatedAt: string;
};

export type StockRow = {
  sku: string;
  productName: string;
  quantityOnHand: number;
  quantityReserved: number;
  available: number;
};

export type MovementReason = "In" | "Out" | "Reserve" | "Release" | "Adjust" | "Sale";

export type StartedPayment = { providerRef: string; paymentUrl: string; amount: number };

export type MockPayment = {
  providerRef: string;
  amount: number;
  status: "Pending" | "Succeeded" | "Failed" | "RefundRequired";
  orderId: string;
};

export type StockMovement = {
  id: number;
  sku: string;
  change: number;
  reason: MovementReason;
  refOrderId: number | null;
  note: string | null;
  createdAt: string;
};

export type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };

export type ProductInput = {
  sku?: string;
  name: string;
  description: string | null;
  price: number;
  category: string | null;
  imageUrl: string | null;
  initialQuantity?: number;
  isActive?: boolean;
};

export type CartLine = {
  sku: string;
  name: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  available: number;
  lineTotal: number;
  isAvailable: boolean;
};

export type Cart = { items: CartLine[]; itemCount: number; total: number };

export type OrderStatus = "Pending" | "Paid" | "Cancelled" | "Expired";

export type Order = {
  id: string;
  status: OrderStatus;
  total: number;
  customerName: string;
  customerEmail: string;
  customerPhone: string | null;
  shippingAddress: string | null;
  createdAt: string;
  expiresAt: string;
  paidAt: string | null;
  cancelledAt: string | null;
  items: { sku: string; name: string; unitPrice: number; quantity: number; lineTotal: number }[];
};

export type OrderSummary = {
  id: string;
  status: OrderStatus;
  total: number;
  customerName: string;
  customerEmail: string;
  itemCount: number;
  createdAt: string;
  expiresAt: string;
};

export type CheckoutInput = {
  customerName: string;
  customerEmail: string;
  customerPhone: string | null;
  shippingAddress: string | null;
};

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
    cache: "no-store",
  });
  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const body = await res.json();
      if (body.errors) {
        message = Object.values(body.errors as Record<string, string[]>).flat().join(" ");
      } else if (body.detail || body.title) {
        message = body.detail ?? body.title;
      }
    } catch {
      // response had no JSON body; keep the generic message
    }
    throw new ApiError(message, res.status);
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

function qs(params: Record<string, string | number | boolean | undefined>) {
  const sp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== "") sp.set(k, String(v));
  }
  const s = sp.toString();
  return s ? `?${s}` : "";
}

export const api = {
  listProducts: (p: { search?: string; includeInactive?: boolean; page?: number; pageSize?: number }) =>
    request<Paged<Product>>(`/api/products${qs(p)}`),
  createProduct: (body: ProductInput) =>
    request<Product>("/api/products", { method: "POST", body: JSON.stringify(body) }),
  updateProduct: (sku: string, body: ProductInput) =>
    request<Product>(`/api/products/${encodeURIComponent(sku)}`, { method: "PUT", body: JSON.stringify(body) }),
  deactivateProduct: (sku: string) =>
    request<void>(`/api/products/${encodeURIComponent(sku)}`, { method: "DELETE" }),

  listStocks: (p: { lowStockOnly?: boolean; lowStockThreshold?: number; page?: number; pageSize?: number }) =>
    request<Paged<StockRow>>(`/api/stocks${qs(p)}`),
  adjustStock: (sku: string, body: { change: number; reason: MovementReason; note: string | null }) =>
    request<StockRow>(`/api/stocks/${encodeURIComponent(sku)}/adjust`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  getCart: () => request<Cart>("/api/cart"),
  addToCart: (sku: string, quantity = 1) =>
    request<Cart>("/api/cart/items", { method: "POST", body: JSON.stringify({ sku, quantity }) }),
  setCartQuantity: (sku: string, quantity: number) =>
    request<Cart>(`/api/cart/items/${encodeURIComponent(sku)}`, { method: "PUT", body: JSON.stringify({ quantity }) }),
  removeFromCart: (sku: string) =>
    request<Cart>(`/api/cart/items/${encodeURIComponent(sku)}`, { method: "DELETE" }),
  checkout: (body: CheckoutInput) =>
    request<Order>("/api/checkout", { method: "POST", body: JSON.stringify(body) }),

  getOrder: (id: string) => request<Order>(`/api/orders/${id}`),
  cancelOrder: (id: string) => request<Order>(`/api/orders/${id}/cancel`, { method: "POST" }),
  startPayment: (orderId: string) => request<StartedPayment>(`/api/orders/${orderId}/pay`, { method: "POST" }),
  getMockPayment: (ref: string) => request<MockPayment>(`/api/mock-provider/payments/${encodeURIComponent(ref)}`),
  simulateMockPayment: (ref: string, outcome: "succeeded" | "failed") =>
    request<MockPayment>(`/api/mock-provider/payments/${encodeURIComponent(ref)}/simulate`, {
      method: "POST",
      body: JSON.stringify({ outcome }),
    }),
  listOrders: (p: { status?: OrderStatus; page?: number; pageSize?: number }) =>
    request<Paged<OrderSummary>>(`/api/orders${qs(p)}`),

  listMovements: (sku: string, page = 1) =>
    request<Paged<StockMovement>>(`/api/stocks/${encodeURIComponent(sku)}/movements${qs({ page })}`),
};
