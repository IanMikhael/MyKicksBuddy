(() => {
    "use strict";
    const sidebar = document.querySelector("[data-staff-sidebar]");
    const backdrop = document.querySelector("[data-staff-close-nav]");
    const setOpen = (open) => {
        sidebar?.classList.toggle("is-open", open);
        if (backdrop) backdrop.hidden = !open;
        document.body.classList.toggle("staff-nav-open", open);
    };
    document.querySelector("[data-staff-open-nav]")?.addEventListener("click", () => setOpen(true));
    backdrop?.addEventListener("click", () => setOpen(false));
    document.addEventListener("keydown", (event) => { if (event.key === "Escape") setOpen(false); });
})();
