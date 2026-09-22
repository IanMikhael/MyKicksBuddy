(() => {
    "use strict";

    const forms = document.querySelectorAll("[data-auth-form]");

    document.querySelectorAll("[data-password-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            const input = document.getElementById(button.dataset.passwordToggle);
            if (!input) return;

            const reveal = input.type === "password";
            input.type = reveal ? "text" : "password";
            button.classList.toggle("is-revealed", reveal);
            button.setAttribute("aria-pressed", String(reveal));
            button.setAttribute("aria-label", reveal ? "Sembunyikan kata sandi" : "Tampilkan kata sandi");
        });
    });

    const normalizeKey = (key) => {
        const cleanKey = String(key || "").split(".").pop();
        return cleanKey === "LoginPassword" || cleanKey === "RegisterPassword" ? "Password" : cleanKey;
    };

    const extractMessages = (value) => {
        if (typeof value === "string") return value.trim() ? [value] : [];
        if (Array.isArray(value)) return value.flatMap(extractMessages);
        if (!value || typeof value !== "object") return [];
        if (typeof value.errorMessage === "string") return extractMessages(value.errorMessage);
        if (Array.isArray(value.errors)) return extractMessages(value.errors);
        return [];
    };

    const getErrorMessages = (payload) => {
        if (!payload || typeof payload !== "object") return [];

        const source = payload.errors && typeof payload.errors === "object"
            ? payload.errors
            : payload;

        return Object.entries(source)
            .filter(([key]) => key.toLowerCase() !== "message")
            .flatMap(([key, value]) => {
                return extractMessages(value)
                    .map((message) => ({ key: normalizeKey(key), message }));
            });
    };

    const readResponse = async (response) => {
        const text = await response.text();
        if (!text) return {};

        try {
            return JSON.parse(text);
        } catch {
            return { message: text };
        }
    };

    const getDestination = (form, payload) => {
        if (form.dataset.authForm === "register") {
            return form.dataset.customerUrl || "";
        }

        const role = String(payload.role || "").trim().toLowerCase();
        if (role === "customer") {
            const requested = new URLSearchParams(window.location.search).get("ReturnUrl");
            if (requested && requested.startsWith("/") && !requested.startsWith("//") &&
                /^\/(customer|orders|addresses)(\/|$|\?)/i.test(requested)) {
                return requested;
            }
            return form.dataset.customerUrl || "";
        }
        if (role === "kasir") return form.dataset.staffUrl || "";
        if (role === "admin") return form.dataset.adminUrl || "";
        return "";
    };

    forms.forEach((form) => {
        const formType = form.dataset.authForm;
        const alert = form.querySelector("[data-form-alert]");
        const submitButton = form.querySelector("[data-submit-button]");
        const submitLabel = form.querySelector("[data-submit-label]");
        const idleLabel = formType === "register" ? "Daftar" : "Masuk";
        const busyLabel = formType === "register" ? "Mendaftarkan..." : "Memproses...";

        const showAlert = (message, success = false) => {
            if (!alert) return;
            alert.textContent = message;
            alert.classList.toggle("is-success", success);
            alert.hidden = false;
        };

        const clearAlert = () => {
            if (!alert) return;
            alert.hidden = true;
            alert.textContent = "";
            alert.classList.remove("is-success");
        };

        const fieldFor = (key) => {
            if (key === "ConfirmPassword") return form.querySelector("#ConfirmPassword");
            return form.elements.namedItem(key);
        };

        const setFieldError = (key, message) => {
            const normalizedKey = normalizeKey(key);
            const input = fieldFor(normalizedKey);
            const error = form.querySelector(`[data-error-for="${normalizedKey}"]`);

            if (input instanceof HTMLElement) {
                input.classList.add("is-invalid");
                input.setAttribute("aria-invalid", "true");
            }
            if (error) error.textContent = message;
        };

        const clearErrors = () => {
            clearAlert();
            form.querySelectorAll(".is-invalid").forEach((input) => {
                input.classList.remove("is-invalid");
                input.removeAttribute("aria-invalid");
            });
            form.querySelectorAll("[data-error-for]").forEach((error) => {
                error.textContent = "";
            });
            const contactError = form.querySelector("[data-contact-error]");
            contactError?.classList.remove("is-error");
        };

        const setLoading = (loading) => {
            submitButton.disabled = loading;
            submitButton.setAttribute("aria-busy", String(loading));
            submitLabel.textContent = loading ? busyLabel : idleLabel;
            form.querySelectorAll("input, .password-toggle").forEach((element) => {
                element.disabled = loading;
            });
        };

        const validate = () => {
            let valid = true;

            if (formType === "login") {
                const identifier = form.elements.namedItem("EmailOrPhone");
                if (!identifier.value.trim()) {
                    setFieldError("EmailOrPhone", "Email atau nomor ponsel wajib diisi.");
                    valid = false;
                }
            }

            if (formType === "register") {
                const fullName = form.elements.namedItem("FullName");
                const email = form.elements.namedItem("Email");
                const phone = form.elements.namedItem("Phone");
                const password = form.elements.namedItem("Password");
                const confirmPassword = form.querySelector("#ConfirmPassword");

                if (!fullName.value.trim()) {
                    setFieldError("FullName", "Nama lengkap wajib diisi.");
                    valid = false;
                }

                if (!email.value.trim() && !phone.value.trim()) {
                    email.classList.add("is-invalid");
                    email.setAttribute("aria-invalid", "true");
                    phone.classList.add("is-invalid");
                    phone.setAttribute("aria-invalid", "true");
                    form.querySelector("[data-contact-error]")?.classList.add("is-error");
                    valid = false;
                }

                if (email.value.trim() && email.validity.typeMismatch) {
                    setFieldError("Email", "Masukkan format email yang valid.");
                    valid = false;
                }

                if (!password.value) {
                    setFieldError("Password", "Password wajib diisi.");
                    valid = false;
                } else if (password.value.length < 6) {
                    setFieldError("Password", "Password minimal 6 karakter.");
                    valid = false;
                }

                if (!confirmPassword.value) {
                    setFieldError("ConfirmPassword", "Konfirmasi password wajib diisi.");
                    valid = false;
                } else if (confirmPassword.value !== password.value) {
                    setFieldError("ConfirmPassword", "Konfirmasi password tidak sama.");
                    valid = false;
                }
            } else {
                const password = form.elements.namedItem("Password");
                if (!password.value) {
                    setFieldError("Password", "Password wajib diisi.");
                    valid = false;
                }
            }

            if (!valid) {
                form.querySelector(".is-invalid")?.focus();
            }
            return valid;
        };

        form.addEventListener("input", (event) => {
            const input = event.target;
            if (!(input instanceof HTMLInputElement)) return;

            input.classList.remove("is-invalid");
            input.removeAttribute("aria-invalid");
            const key = input.id === "ConfirmPassword" ? "ConfirmPassword" : input.name;
            const error = form.querySelector(`[data-error-for="${key}"]`);
            if (error) error.textContent = "";

            if (formType === "register" && (input.name === "Email" || input.name === "Phone")) {
                const email = form.elements.namedItem("Email");
                const phone = form.elements.namedItem("Phone");
                if (email.value.trim() || phone.value.trim()) {
                    email.classList.remove("is-invalid");
                    email.removeAttribute("aria-invalid");
                    phone.classList.remove("is-invalid");
                    phone.removeAttribute("aria-invalid");
                    form.querySelector("[data-contact-error]")?.classList.remove("is-error");
                }
            }
        });

        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            clearErrors();
            if (!validate()) return;

            const body = new URLSearchParams();
            new FormData(form).forEach((value, key) => {
                const cleanValue = key === "Password" ? String(value) : String(value).trim();
                body.append(key, cleanValue);
            });

            setLoading(true);
            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: body.toString()
                });
                const payload = await readResponse(response);

                if (!response.ok) {
                    const fieldErrors = getErrorMessages(payload);
                    fieldErrors.forEach(({ key, message }) => setFieldError(key, message));

                    if (payload.message) {
                        showAlert(payload.message);
                    } else if (fieldErrors.length) {
                        showAlert("Periksa kembali data yang kamu masukkan.");
                        form.querySelector(".is-invalid")?.focus();
                    } else {
                        showAlert("Permintaan tidak dapat diproses. Silakan coba lagi.");
                    }
                    return;
                }

                const destination = getDestination(form, payload);
                if (destination) {
                    window.location.assign(destination);
                    return;
                }

                showAlert("Autentikasi berhasil. Halaman tujuan akun belum tersedia.", true);
            } catch {
                showAlert("Tidak dapat terhubung ke server. Periksa koneksi lalu coba lagi.");
            } finally {
                setLoading(false);
            }
        });
    });
})();
