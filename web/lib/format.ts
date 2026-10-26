const money = new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB" });

export const formatMoney = (n: number) => money.format(n);

/** Tell the shop header that the cart changed so it can refetch the item count. */
export const notifyCartChanged = () => window.dispatchEvent(new Event("cart:changed"));
