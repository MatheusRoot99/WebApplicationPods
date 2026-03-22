(() => {
    "use strict";

    const STORAGE_KEY = "produtos_adicionados_carrinho";

    document.addEventListener("DOMContentLoaded", () => {
        initCartUI();

        setTimeout(() => {
            document.querySelectorAll("#carrinho-alerts .alert").forEach((alert) => {
                alert.style.transition = "opacity 0.5s";
                alert.style.opacity = "0";
                setTimeout(() => alert.remove(), 500);
            });
        }, 5000);
    });

    const qs = (s, r = document) => r.querySelector(s);
    const qsa = (s, r = document) => Array.from(r.querySelectorAll(s));

    const parseCurrency = (str) => {
        if (!str) return 0;
        return parseFloat(String(str).replace(/[^\d,]/g, "").replace(",", ".")) || 0;
    };

    const clampByMinMax = (input, value) => {
        const min = parseInt(input.getAttribute("min")) || 1;
        const max = parseInt(input.getAttribute("max")) || 999;
        return Math.min(Math.max(value, min), max);
    };

    const getAFToken = (form) =>
        (typeof window.getAntiForgeryToken === "function" && window.getAntiForgeryToken()) ||
        form?.querySelector('input[name="__RequestVerificationToken"]')?.value ||
        qs('input[name="__RequestVerificationToken"]')?.value ||
        "";

    const getStorage = () => {
        try {
            const data = localStorage.getItem(STORAGE_KEY);
            if (!data) return [];
            const parsed = JSON.parse(data);
            return Array.isArray(parsed) ? parsed.map(String) : [];
        } catch {
            return [];
        }
    };

    const saveStorage = (ids) => {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify([...new Set(ids.map(String))]));
        } catch { }
    };

    const removeFromStorage = (produtoId) => {
        if (!produtoId) return;
        const ids = getStorage().filter(x => x !== String(produtoId));
        saveStorage(ids);
    };

    const setTotalAnimated = (newTotalString) => {
        const totalEl = qs(".total-valor");
        if (!totalEl || !newTotalString) return;

        const oldVal = parseCurrency(totalEl.textContent);
        const newVal = parseCurrency(newTotalString);

        totalEl.textContent = newTotalString;

        if (newVal > oldVal) {
            totalEl.classList.add("highlight-update");
            totalEl.classList.remove("highlight-remove");
        } else if (newVal < oldVal) {
            totalEl.classList.add("highlight-remove");
            totalEl.classList.remove("highlight-update");
        } else {
            totalEl.classList.remove("highlight-update", "highlight-remove");
        }

        setTimeout(() => {
            totalEl.classList.remove("highlight-update", "highlight-remove");
        }, 1000);
    };

    const updateBadges = (count) => {
        if (typeof window.updateCartBadges === "function") {
            window.updateCartBadges(count);
        }
    };

    const showAlert = (type, message) => {
        const alerts = qs("#carrinho-alerts");
        if (!alerts) return;

        qsa(`.alert-${type}`, alerts).forEach((a) => a.remove());

        const div = document.createElement("div");
        div.className = `alert alert-${type === "success" ? "success" : "danger"} alert-dismissible fade show`;
        div.innerHTML = `
            <i class="fas ${type === "success" ? "fa-check-circle" : "fa-exclamation-circle"} me-1"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Fechar"></button>
        `;
        alerts.appendChild(div);

        if (window.bootstrap?.Alert) new bootstrap.Alert(div);

        setTimeout(() => {
            if (div.parentNode) {
                const bs = window.bootstrap?.Alert?.getOrCreateInstance(div);
                bs ? bs.close() : div.remove();
            }
        }, 5000);
    };

    const toggleEmptyStateIfNeeded = (isEmptyHint = null) => {
        const noItems =
            typeof isEmptyHint === "boolean"
                ? isEmptyHint
                : qsa(".linha-item").length === 0;

        const resumo = qs("#resumo-card");
        const empty = qs("#empty-state");
        const pop = qs("#populares-wrapper");

        if (noItems) {
            resumo?.classList.add("d-none");
            empty?.classList.remove("d-none");
            pop?.classList.remove("d-none");
        } else {
            resumo?.classList.remove("d-none");
            empty?.classList.add("d-none");
            pop?.classList.add("d-none");
        }
    };

    const mustReload = (resp) => {
        const ct = resp.headers.get("content-type");
        return !ct || !ct.includes("application/json");
    };

    const handleQuantityUpdate = async (form, newValue) => {
        const formData = new FormData(form);
        formData.set("quantidade", newValue);

        try {
            const resp = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "RequestVerificationToken": getAFToken(form),
                },
                credentials: "same-origin"
            });

            if (mustReload(resp)) {
                window.location.reload();
                return;
            }

            const result = await resp.json();

            if (result.ok) {
                updateBadges(result.count);
                if (result.total) setTotalAnimated(result.total);
                toggleEmptyStateIfNeeded(result.isEmpty);
                showAlert("success", result.message || "Quantidade atualizada com sucesso");
            } else {
                if ((result.error || "").toLowerCase().includes("não encontrado")) {
                    window.location.reload();
                    return;
                }

                showAlert("danger", result.error || "Erro ao atualizar quantidade");

                const input = form.querySelector(".quantidade-input");
                if (input) input.value = input.getAttribute("data-old-value") || input.value;
            }
        } catch (err) {
            console.error("Erro ao atualizar quantidade:", err);
            showAlert("danger", "Erro de conexão ao atualizar quantidade");

            const input = form.querySelector(".quantidade-input");
            if (input) input.value = input.getAttribute("data-old-value") || input.value;
        }
    };

    const handleRemove = async (form) => {
        const formData = new FormData(form);

        try {
            const resp = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "RequestVerificationToken": getAFToken(form),
                },
                credentials: "same-origin"
            });

            if (mustReload(resp)) {
                const produtoIdFallback = form.querySelector('input[name="produtoId"]')?.value;
                removeFromStorage(produtoIdFallback);
                window.location.reload();
                return;
            }

            const result = await resp.json();

            if (result.ok) {
                const produtoId = form.querySelector('input[name="produtoId"]')?.value;
                removeFromStorage(produtoId);

                updateBadges(result.count);

                const row = form.closest(".linha-item");
                if (row) {
                    row.style.transition = "opacity .3s, transform .3s";
                    row.style.opacity = "0";
                    row.style.transform = "translateX(-100px)";

                    setTimeout(() => {
                        row.remove();
                        if (result.total) setTotalAnimated(result.total);
                        toggleEmptyStateIfNeeded(result.isEmpty);
                    }, 300);
                } else {
                    if (result.total) setTotalAnimated(result.total);
                    toggleEmptyStateIfNeeded(result.isEmpty);
                }

                showAlert("success", result.message || "Item removido com sucesso");
            } else {
                if ((result.error || "").toLowerCase().includes("não encontrado")) {
                    window.location.reload();
                    return;
                }

                showAlert("danger", result.error || "Erro ao remover item");
            }
        } catch (err) {
            console.error("Erro ao remover item:", err);
            showAlert("danger", "Erro de conexão ao remover item");
            setTimeout(() => window.location.reload(), 1000);
        }
    };

    function initCartUI() {
        document.addEventListener("click", (e) => {
            const btn = e.target.closest(".quantidade-form .btn-outline");
            if (!btn) return;

            e.preventDefault();

            const form = btn.closest("form");
            const input = form?.querySelector(".quantidade-input");
            if (!form || !input) return;

            const currentValue = parseInt(input.value) || 0;
            let newValue = currentValue;

            if (btn.querySelector(".fa-plus")) newValue = currentValue + 1;
            if (btn.querySelector(".fa-minus")) newValue = currentValue - 1;

            newValue = clampByMinMax(input, newValue);

            if (newValue !== currentValue) {
                input.setAttribute("data-old-value", input.value);
                input.value = newValue;
                handleQuantityUpdate(form, newValue);
            }
        });

        document.addEventListener("click", (e) => {
            const btn = e.target.closest(".quantidade-form .btn-primary");
            if (!btn) return;

            e.preventDefault();

            const form = btn.closest("form");
            const input = form?.querySelector(".quantidade-input");
            if (!form || !input) return;

            const value = parseInt(input.value) || 1;
            input.setAttribute("data-old-value", input.value);
            handleQuantityUpdate(form, value);
        });

        document.addEventListener("change", (e) => {
            const input = e.target.closest(".quantidade-input");
            if (!input) return;

            const form = input.closest("form");
            let value = parseInt(input.value) || 1;
            value = clampByMinMax(input, value);

            input.setAttribute("data-old-value", input.value);
            input.value = value;

            handleQuantityUpdate(form, value);
        });

        document.addEventListener("keydown", (e) => {
            const input = e.target.closest(".quantidade-input");
            if (!input) return;

            if (e.key === "Enter") {
                e.preventDefault();
                input.setAttribute("data-old-value", input.value);
                input.dispatchEvent(new Event("change"));
            }
        });

        document.addEventListener("focusin", (e) => {
            const input = e.target.closest(".quantidade-input");
            if (input) input.setAttribute("data-old-value", input.value);
        });

        document.addEventListener("submit", (e) => {
            const form = e.target.closest(".remove-form");
            if (!form) return;

            e.preventDefault();

            if (!confirm("Tem certeza que deseja remover este item do carrinho?")) return;

            handleRemove(form);
        });
    }
})();