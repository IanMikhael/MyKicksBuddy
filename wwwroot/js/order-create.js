(() => {
    "use strict";
    const form = document.querySelector("[data-order-form]");
    if (!form) return;

    const pickupPanel = form.querySelector("[data-pickup-addresses]");
    const serviceSummary = form.querySelector("[data-summary-service]");
    const quantitySummary = form.querySelector("[data-summary-quantity]");
    const totalSummary = form.querySelector("[data-summary-total]");
    const subtotalSummary = form.querySelector("[data-summary-subtotal]");
    const quantityInput = form.querySelector("[data-order-quantity]");
    const descriptionInput = form.querySelector('[name="ShoeDescription"]');
    const submitButton = form.querySelector('button[type="submit"]');
    const currency = new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 0 });

    const refreshFulfillment = () => {
        const selected = form.querySelector("[data-fulfillment]:checked")?.value;
        pickupPanel.hidden = selected !== "pickup_delivery";
        if (pickupPanel.hidden) {
            pickupPanel.querySelectorAll('input[name="AddressId"]').forEach(input => { input.checked = false; });
        }
    };

    const refreshSummary = () => {
        const service = form.querySelector('input[name="ServiceId"]:checked');
        const quantity = Math.max(1, Number(quantityInput?.value || 1));
        const price = Number(service?.dataset.servicePrice || 0);
        serviceSummary.textContent = service?.dataset.serviceName || "Belum dipilih";
        quantitySummary.textContent = `${quantity} pasang`;
        totalSummary.textContent = currency.format(price * quantity);
        subtotalSummary.textContent = currency.format(price * quantity);
        const fulfillment = form.querySelector("[data-fulfillment]:checked")?.value;
        const addressValid = fulfillment !== "pickup_delivery" || Boolean(form.querySelector('input[name="AddressId"]:checked'));
        const valid = Boolean(service && descriptionInput?.value.trim() && Number(quantityInput?.value) >= 1 && Number(quantityInput?.value) <= 20 && fulfillment && addressValid);
        submitButton.disabled = !valid;
    };

    form.querySelectorAll("[data-fulfillment]").forEach(input => input.addEventListener("change", () => { refreshFulfillment(); refreshSummary(); }));
    form.querySelectorAll('input[name="ServiceId"]').forEach(input => input.addEventListener("change", refreshSummary));
    form.querySelectorAll('input[name="AddressId"]').forEach(input => input.addEventListener("change", refreshSummary));
    quantityInput?.addEventListener("input", refreshSummary);
    descriptionInput?.addEventListener("input", refreshSummary);
    refreshFulfillment();
    refreshSummary();
})();
