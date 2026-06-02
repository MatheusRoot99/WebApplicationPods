(() => {
    "use strict";

    const STORAGE_KEY = "produtos_adicionados_carrinho";

    const qs = (selector, root = document) => root.querySelector(selector);
    const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));

    document.addEventListener("DOMContentLoaded", () => {
        initCartUI();
        autoHideAlerts();
    });

    const parseCurrency = (value) => {
        if (!value) return 0;

        return parseFloat(
            String(value)
                .replace(/[^\d,.-]/g, "")
                .replace(/\./g, "")
                .replace(",", ".")
        ) || 0;
    };

    const clampByMinMax = (input, value) => {
        const min = parseInt(input.getAttribute("min"), 10) || 1;
        const max = parseInt(input.getAttribute("max"), 10) || 999;

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
            localStorage.setItem(
                STORAGE_KEY,
                JSON.stringify([...new Set((ids || []).map(String))])
            );
        } catch {
            // localStorage pode falhar em alguns navegadores/modos privados.
        }
    };

    const removeFromStorage = (produtoId) => {
        if (!produtoId) return;

        const ids = getStorage().filter(id => id !== String(produtoId));
        saveStorage(ids);
    };

    const autoHideAlerts = () => {
        setTimeout(() => {
            qsa("#carrinho-alerts .alert").forEach((alert) => {
                alert.style.transition = "opacity .45s ease";
                alert.style.opacity = "0";

                setTimeout(() => alert.remove(), 450);
            });
        }, 4500);
    };

    const setTotalAnimated = (newTotalString) => {
        const totalEl =
            qs(".total-valor") ||
            qs("[data-cart-total]") ||
            qs("#totalCarrinho");

        if (!totalEl || !newTotalString) return;

        const oldValue = parseCurrency(totalEl.textContent);
        const newValue = parseCurrency(newTotalString);

        totalEl.textContent = newTotalString;

        totalEl.classList.remove("highlight-update", "highlight-remove");

        if (newValue > oldValue) {
            totalEl.classList.add("highlight-update");
        } else if (newValue < oldValue) {
            totalEl.classList.add("highlight-remove");
        }

        setTimeout(() => {
            totalEl.classList.remove("highlight-update", "highlight-remove");
        }, 900);
    };

    const updateBadges = (count) => {
        const normalized = Number(count || 0);

        if (typeof window.updateCartBadges === "function") {
            window.updateCartBadges(normalized);
        }

        qsa("[data-cart-count], .cart-count, #cartCount").forEach((el) => {
            el.textContent = normalized;
        });
    };

    const showAlert = (type, message) => {
        const alerts = qs("#carrinho-alerts");
        if (!alerts || !message) return;

        const cssType = type === "success" ? "success" : "danger";
        qsa(`.alert-${cssType}`, alerts).forEach((alert) => alert.remove());

        const div = document.createElement("div");
        div.className = `alert alert-${cssType} alert-dismissible fade show`;
        div.innerHTML = `
            <i class="fas ${cssType === "success" ? "fa-check-circle" : "fa-exclamation-circle"} me-1"></i>
            ${escapeHtml(message)}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Fechar"></button>
        `;

        alerts.appendChild(div);

        if (window.bootstrap?.Alert) {
            new bootstrap.Alert(div);
        }

        setTimeout(() => {
            if (!div.parentNode) return;

            const bs = window.bootstrap?.Alert?.getOrCreateInstance(div);
            bs ? bs.close() : div.remove();
        }, 4200);
    };

    const escapeHtml = (value) => {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    };

    const mustReload = (response) => {
        const contentType = response.headers.get("content-type") || "";
        return !contentType.includes("application/json");
    };

    const reloadIfEmpty = (result) => {
        if (result?.isEmpty === true || Number(result?.count ?? 1) <= 0) {
            window.location.reload();
            return true;
        }

        return false;
    };

    const getQtyInput = (form) =>
        form?.querySelector(".cart-qty-input") ||
        form?.querySelector(".quantidade-input");

    const getItemCard = (element) =>
        element?.closest(".cart-item-card") ||
        element?.closest(".linha-item");

    const setButtonLoading = (button, loading) => {
        if (!button) return;

        if (loading) {
            if (!button.dataset.originalHtml) {
                button.dataset.originalHtml = button.innerHTML;
            }

            button.disabled = true;
            button.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
            return;
        }

        button.disabled = false;

        if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
        }
    };

    const pulseCard = (form) => {
        const card = getItemCard(form);
        if (!card) return;

        card.classList.remove("is-updated");
        void card.offsetWidth;
        card.classList.add("is-updated");

        setTimeout(() => {
            card.classList.remove("is-updated");
        }, 650);
    };

    const handleQuantityUpdate = async (form, newValue, button = null) => {
        const formData = new FormData(form);
        formData.set("quantidade", newValue);

        setButtonLoading(button, true);

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "RequestVerificationToken": getAFToken(form)
                },
                credentials: "same-origin"
            });

            if (mustReload(response)) {
                window.location.reload();
                return;
            }

            const result = await response.json();

            if (result.ok) {
                updateBadges(result.count);

                if (result.total) {
                    setTotalAnimated(result.total);
                }

                pulseCard(form);

                if (reloadIfEmpty(result)) {
                    return;
                }

                return;
            }

            const error = result.error || "Erro ao atualizar quantidade";

            if (error.toLowerCase().includes("não encontrado")) {
                window.location.reload();
                return;
            }

            showAlert("danger", error);
            restoreOldQuantity(form);
        } catch (error) {
            console.error("Erro ao atualizar quantidade:", error);
            showAlert("danger", "Erro de conexão ao atualizar quantidade");
            restoreOldQuantity(form);
        } finally {
            setButtonLoading(button, false);
        }
    };

    const restoreOldQuantity = (form) => {
        const input = getQtyInput(form);
        if (!input) return;

        input.value = input.getAttribute("data-old-value") || input.value;
    };

    const handleRemove = async (form, button = null) => {
        const formData = new FormData(form);
        const produtoIdFallback = form.querySelector('input[name="produtoId"]')?.value;

        setButtonLoading(button, true);

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "RequestVerificationToken": getAFToken(form)
                },
                credentials: "same-origin"
            });

            if (mustReload(response)) {
                removeFromStorage(produtoIdFallback);
                window.location.reload();
                return;
            }

            const result = await response.json();

            if (result.ok) {
                const produtoId = form.querySelector('input[name="produtoId"]')?.value;
                removeFromStorage(produtoId);

                updateBadges(result.count);

                const card = getItemCard(form);

                if (card) {
                    card.style.transition = "opacity .25s ease, transform .25s ease";
                    card.style.opacity = "0";
                    card.style.transform = "translateX(-24px)";

                    setTimeout(() => {
                        card.remove();

                        if (result.total) {
                            setTotalAnimated(result.total);
                        }

                        reloadIfEmpty(result);
                    }, 250);
                } else {
                    if (result.total) {
                        setTotalAnimated(result.total);
                    }

                    reloadIfEmpty(result);
                }

                showAlert("success", result.message || "Item removido do carrinho");
                return;
            }

            const error = result.error || "Erro ao remover item";

            if (error.toLowerCase().includes("não encontrado")) {
                window.location.reload();
                return;
            }

            showAlert("danger", error);
        } catch (error) {
            console.error("Erro ao remover item:", error);
            showAlert("danger", "Erro de conexão ao remover item");

            setTimeout(() => {
                window.location.reload();
            }, 1000);
        } finally {
            setButtonLoading(button, false);
        }
    };

    function initCartUI() {
        document.addEventListener("click", (event) => {
            const button = event.target.closest(".cart-qty-form .cart-qty-btn, .quantidade-form .btn-outline");
            if (!button) return;

            event.preventDefault();

            const form = button.closest("form");
            const input = getQtyInput(form);

            if (!form || !input) return;

            const currentValue = parseInt(input.value, 10) || 1;
            let newValue = currentValue;

            const op = button.getAttribute("value") || button.value || "";

            if (op === "inc" || button.querySelector(".fa-plus")) {
                newValue = currentValue + 1;
            }

            if (op === "dec" || button.querySelector(".fa-minus")) {
                newValue = currentValue - 1;
            }

            newValue = clampByMinMax(input, newValue);

            if (newValue === currentValue) return;

            input.setAttribute("data-old-value", input.value);
            input.value = newValue;

            handleQuantityUpdate(form, newValue, button);
        });

        document.addEventListener("change", (event) => {
            const input = event.target.closest(".cart-qty-input, .quantidade-input");
            if (!input) return;

            const form = input.closest("form");
            if (!form) return;

            let value = parseInt(input.value, 10) || 1;
            value = clampByMinMax(input, value);

            input.setAttribute("data-old-value", input.value);
            input.value = value;

            handleQuantityUpdate(form, value);
        });

        document.addEventListener("keydown", (event) => {
            const input = event.target.closest(".cart-qty-input, .quantidade-input");
            if (!input) return;

            if (event.key === "Enter") {
                event.preventDefault();
                input.setAttribute("data-old-value", input.value);
                input.dispatchEvent(new Event("change", { bubbles: true }));
            }
        });

        document.addEventListener("focusin", (event) => {
            const input = event.target.closest(".cart-qty-input, .quantidade-input");
            if (!input) return;

            input.setAttribute("data-old-value", input.value);
        });

        document.addEventListener("submit", (event) => {
            const qtyForm = event.target.closest(".cart-qty-form, .quantidade-form");

            if (qtyForm) {
                event.preventDefault();

                const input = getQtyInput(qtyForm);
                if (!input) return;

                let value = parseInt(input.value, 10) || 1;
                value = clampByMinMax(input, value);

                input.setAttribute("data-old-value", input.value);
                input.value = value;

                handleQuantityUpdate(qtyForm, value);
                return;
            }

            const removeForm = event.target.closest(".cart-remove-form, .remove-form");
            if (!removeForm) return;

            event.preventDefault();

            if (!confirm("Remover este item do carrinho?")) {
                return;
            }

            const button = removeForm.querySelector("button[type='submit']");
            handleRemove(removeForm, button);
        });
    }
})();