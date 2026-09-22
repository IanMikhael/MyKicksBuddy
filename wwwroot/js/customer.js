(() => {
  "use strict";

  const $ = (selector) => document.querySelector(selector);
  const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  })[char]);
  const statusTone = (group) => group === "completed" ? "is-success" : group === "cancelled" ? "is-danger" : "";
  const dateLabel = (value) => {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Tanggal belum tersedia" : new Intl.DateTimeFormat("id-ID", { day: "numeric", month: "short", year: "numeric" }).format(date);
  };
  const money = (value) => new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 }).format(Number(value ?? 0));
  const progressMarkup = (current) => {
    return `<div class="customer-progress" style="--progress:${current}">${["Dibuat", "Dijemput", "Diproses", "Selesai"].map((label, index) => `<span class="customer-progress-step ${index < current ? "done" : ""} ${index === current ? "current" : ""}"><i></i>${label}</span>`).join("")}</div>`;
  };

  const activeCard = (order, detail) => {
    const service = detail?.items?.[0]?.serviceName ?? order.serviceName ?? "Pesanan perawatan";
    const href = `/orders/${order.id}/detail`;
    return `<article class="customer-active-card"><div class="customer-active-top"><span class="customer-active-icon"><img src="/images/customer/figma/active-order-shoe.svg" alt="" /></span><div><div class="customer-order-name">${escapeHtml(service)}</div><div class="customer-order-code">${escapeHtml(order.orderCode)}</div></div><span class="customer-status-badge ${statusTone(order.historyGroup)}">${escapeHtml(order.statusLabel)}</span></div><div class="customer-progress-area">${progressMarkup(order.progressStage)}<p class="customer-progress-copy">${escapeHtml(order.progressDescription)}</p></div><a class="customer-order-action" href="${href}">Lihat Status Pesanan <img src="/images/customer/figma/arrow-right.svg" alt="" /></a></article>`;
  };

  async function loadOrders() {
    const active = $("[data-active-orders]");
    const recent = $("[data-recent-orders]");
    if (!active || !recent) return;
    try {
      const response = await fetch("/orders", { credentials: "same-origin" });
      if (!response.ok) throw new Error("orders-request-failed");
      const orders = await response.json();
      const current = orders.find((order) => order.isActive);
      let detail = null;
      if (current) {
        const detailResponse = await fetch(`/orders/${current.id}`, { credentials: "same-origin" });
        if (detailResponse.ok) detail = await detailResponse.json();
      }
      active.innerHTML = current ? activeCard(current, detail) : '<article class="customer-active-empty">Belum ada pesanan aktif. Buat pesanan baru saat layanan telah dipilih.</article>';
      recent.innerHTML = orders.length ? orders.slice(0, 2).map((order) => `<a class="customer-recent-item" href="/orders/${order.id}/detail"><span class="customer-recent-icon"><img src="/images/customer/figma/recent-order.svg" alt="" /></span><span><strong class="customer-recent-name">${escapeHtml(order.serviceName ?? "Pesanan perawatan")}</strong><small class="customer-recent-date">${dateLabel(order.createdAt)} · ${escapeHtml(order.orderCode)}</small></span><span class="customer-recent-right"><b class="customer-status-badge ${statusTone(order.historyGroup)}">${escapeHtml(order.statusLabel)}</b><img src="/images/customer/figma/chevron-right.svg" alt="" /></span></a>`).join("") : '<article class="customer-recent-empty">Belum ada pesanan terakhir.</article>';
    } catch {
      active.innerHTML = '<article class="customer-active-empty is-error">Pesanan belum dapat dimuat. Silakan coba lagi.</article>';
      recent.innerHTML = '<article class="customer-recent-empty is-error">Riwayat belum dapat dimuat.</article>';
    } finally {
      active.setAttribute("aria-busy", "false");
      recent.setAttribute("aria-busy", "false");
    }
  }

  async function loadServices() {
    const container = $("[data-service-list]");
    if (!container) return;
    try {
      const response = await fetch("/api/chatbot/services");
      if (!response.ok) throw new Error("services-request-failed");
      const services = await response.json();
      container.innerHTML = services.length ? services.slice(0, 3).map((service) => `<article class="customer-service-card"><div class="customer-service-image"><img src="/images/customer/figma/service-shoe.svg" alt="" /></div><h3>${escapeHtml(service.name)}</h3><p>${escapeHtml(service.description ?? "Deskripsi layanan belum tersedia.")}</p><div class="customer-service-bottom"><strong>${money(service.price)}</strong><a href="/orders/create?serviceId=${encodeURIComponent(service.id)}">Pilih</a></div></article>`).join("") : '<article class="customer-recent-empty">Layanan belum tersedia.</article>';
    } catch {
      container.innerHTML = '<article class="customer-recent-empty is-error">Layanan belum dapat dimuat.</article>';
    } finally {
      container.setAttribute("aria-busy", "false");
    }
  }

  const drawer = $("[data-customer-drawer]");
  const overlay = $("[data-drawer-backdrop]");
  const drawerToggle = $("[data-drawer-toggle]");
  const drawerClose = $("[data-drawer-close]");
  let priorFocus = null;
  const closeDrawer = () => {
    if (!drawer) return;
    drawer.classList.remove("is-open");
    drawer.setAttribute("aria-hidden", "true");
    drawerToggle?.setAttribute("aria-expanded", "false");
    overlay.hidden = true;
    document.body.classList.remove("customer-drawer-open");
    priorFocus?.focus();
  };
  drawerToggle?.addEventListener("click", () => {
    priorFocus = document.activeElement;
    drawer.classList.add("is-open");
    drawer.setAttribute("aria-hidden", "false");
    drawerToggle.setAttribute("aria-expanded", "true");
    overlay.hidden = false;
    document.body.classList.add("customer-drawer-open");
    drawerClose?.focus();
  });
  drawerClose?.addEventListener("click", closeDrawer);
  overlay?.addEventListener("click", closeDrawer);
  drawer?.addEventListener("keydown", (event) => {
    if (event.key !== "Tab") return;
    const focusables = [...drawer.querySelectorAll('a, button:not([disabled])')];
    const first = focusables[0], last = focusables.at(-1);
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  });
  document.addEventListener("keydown", (event) => { if (event.key === "Escape") closeDrawer(); });

  const accountButton = $("[data-account-toggle]");
  const accountMenu = $("[data-account-menu]");
  accountButton?.addEventListener("click", () => {
    accountMenu.hidden = !accountMenu.hidden;
    accountButton.setAttribute("aria-expanded", String(!accountMenu.hidden));
  });
  document.addEventListener("click", (event) => {
    if (accountMenu && !accountMenu.hidden && !event.target.closest(".customer-account")) {
      accountMenu.hidden = true;
      accountButton?.setAttribute("aria-expanded", "false");
    }
  });

  const help = $("[data-help-dialog]");
  document.querySelectorAll("[data-open-support]").forEach((button) => button.addEventListener("click", () => help?.showModal()));
  $("[data-close-support]")?.addEventListener("click", () => help?.close());

  const search = $("[data-history-search]");
  const filters = [...document.querySelectorAll("[data-history-filter]")];
  const historyCards = [...document.querySelectorAll("[data-history-card]")];
  const historyEmpty = $("[data-history-no-results]");
  let selectedFilter = "all";
  const applyHistoryFilter = () => {
    const query = search?.value.trim().toLocaleLowerCase("id-ID") ?? "";
    let visible = 0;
    historyCards.forEach((card) => {
      const group = card.dataset.historyGroup;
      const matches = (selectedFilter === "all" || selectedFilter === group) && card.dataset.historySearch.includes(query);
      card.hidden = !matches;
      if (matches) visible++;
    });
    if (historyEmpty) historyEmpty.hidden = visible > 0;
  };
  search?.addEventListener("input", applyHistoryFilter);
  filters.forEach((button) => button.addEventListener("click", () => {
    selectedFilter = button.dataset.historyFilter;
    filters.forEach((item) => { item.classList.toggle("is-active", item === button); item.setAttribute("aria-pressed", String(item === button)); });
    applyHistoryFilter();
  }));

  loadOrders();
  loadServices();
})();
