document.addEventListener("DOMContentLoaded", () => {
    /* ------------------------- Obter valores do servidor ------------------------- */
    const productData = document.getElementById('product-data');
    let estoqueMaximo = parseInt(productData?.dataset.stock || "0");
    const initialDisabledState = productData?.dataset.isOutOfStock === 'true';

    /* ------------------------- Tabs com teclado ------------------------- */
    const tabButtons = Array.from(document.querySelectorAll(".tab-button"));
    const panes = {
        description: document.getElementById("tab-description"),
        specifications: document.getElementById("tab-specifications"),
        reviews: document.getElementById("tab-reviews")
    };

    function activateTab(key) {
        tabButtons.forEach(btn => {
            const isActive = btn.dataset.tab === key;
            btn.classList.toggle("active", isActive);
            btn.setAttribute("aria-selected", String(isActive));
        });
        Object.entries(panes).forEach(([k, el]) => {
            const active = k === key;
            el.classList.toggle("active", active);
            el.hidden = !active;
        });
    }

    tabButtons.forEach(btn => btn.addEventListener("click", () => activateTab(btn.dataset.tab)));

    // Navegação por teclado
    document.querySelector(".tabs-header")?.addEventListener("keydown", (e) => {
        const idx = tabButtons.findIndex(b => b.classList.contains("active"));
        if (["ArrowRight", "ArrowLeft"].includes(e.key)) {
            e.preventDefault();
            const next = e.key === "ArrowRight" ? (idx + 1) % tabButtons.length : (idx - 1 + tabButtons.length) % tabButtons.length;
            tabButtons[next].focus();
            tabButtons[next].click();
        }
    });

    /* ------------------------- FAQ Accordion ------------------------- */
    document.querySelectorAll(".faq-question").forEach(btn => {
        btn.addEventListener("click", () => {
            const answer = btn.parentElement.querySelector(".faq-answer");
            const expanded = btn.getAttribute("aria-expanded") === "true";
            btn.setAttribute("aria-expanded", String(!expanded));
            if (!expanded) {
                answer.hidden = false;
                answer.style.maxHeight = answer.scrollHeight + "px";
            } else {
                answer.style.maxHeight = null;
                answer.hidden = true;
            }
        });
    });

    /* ------------------------- Quantidade ------------------------- */
    const minusBtn = document.querySelector(".quantity-btn.minus");
    const plusBtn = document.querySelector(".quantity-btn.plus");
    const quantityInput = document.getElementById("quantity");
    const quantidadeHidden = document.getElementById("quantidadeHidden");

    function clamp(v, min, max) { return Math.min(Math.max(v, min), max); }

    function syncQty(value) {
        const v = clamp(parseInt(value || 1), 1, Math.max(1, estoqueMaximo));
        quantityInput.value = v;
        quantidadeHidden.value = v;
    }

    minusBtn?.addEventListener("click", () => syncQty((+quantityInput.value) - 1));
    plusBtn?.addEventListener("click", () => syncQty((+quantityInput.value) + 1));
    quantityInput?.addEventListener("change", (e) => syncQty(e.target.value));

    /* ------------------------- Sabores & Estoque ------------------------- */
    const radios = Array.from(document.querySelectorAll(".flavor-radio"));
    const saborSelecionado = document.getElementById("saborSelecionado");
    const estoqueInfo = document.getElementById("estoque-info");
    const estoqueQuantidade = document.getElementById("estoque-quantidade");
    const addToCartBtn = document.querySelector(".btn-add-to-cart");
    const buyNowBtn = document.querySelector(".btn-buy-now");

    function setBtnDisabled(disabled) {
        [addToCartBtn, buyNowBtn].forEach(btn => {
            if (!btn) return;
            btn.classList.toggle("disabled", disabled);
            btn.toggleAttribute("disabled", disabled);
            btn.setAttribute("aria-disabled", String(disabled));
            if (addToCartBtn && btn === addToCartBtn) {
                btn.innerHTML = disabled ? '<i class="fas fa-shopping-cart" aria-hidden="true"></i> ESGOTADO' : '<i class="fas fa-shopping-cart" aria-hidden="true"></i> ADICIONAR AO CARRINHO';
            }
        });
    }

    function atualizarEstoquePorSelecao() {
        const selecionado = document.querySelector(".flavor-radio:checked");
        if (selecionado) {
            const estoque = parseInt(selecionado.getAttribute("data-estoque")) || 0;
            estoqueMaximo = estoque;
            if (saborSelecionado) saborSelecionado.value = selecionado.value;
            if (estoqueInfo && estoqueQuantidade) {
                estoqueQuantidade.textContent = String(estoque);
                estoqueInfo.hidden = false;
            }
            syncQty(quantityInput.value);
            setBtnDisabled(estoque <= 0);
        } else {
            estoqueInfo && (estoqueInfo.hidden = true);
            if (saborSelecionado) saborSelecionado.value = "";
            estoqueMaximo = parseInt(productData?.dataset.stock || "0");
            syncQty(quantityInput.value);
            setBtnDisabled(initialDisabledState);
        }
    }

    // Auto-seleciona o primeiro sabor com estoque
    const primeiroComEstoque = radios.find(r => !r.disabled && (parseInt(r.dataset.estoque) || 0) > 0);
    if (primeiroComEstoque) {
        primeiroComEstoque.checked = true;
    }
    radios.forEach(r => r.addEventListener("change", atualizarEstoquePorSelecao));
    atualizarEstoquePorSelecao();

    /* ------------------------- Validação antes de adicionar ------------------------- */
    const addToCartForm = document.getElementById("addToCartForm");
    addToCartForm?.addEventListener("submit", (e) => {
        if (radios.length > 0 && !saborSelecionado.value) {
            e.preventDefault();
            alert("Por favor, selecione um sabor antes de adicionar ao carrinho.");
        }
    });

    /* ------------------------- Compra Rápida ------------------------- */
    buyNowBtn?.addEventListener("click", () => {
        if (buyNowBtn.classList.contains("disabled")) return;
        if (radios.length > 0 && !saborSelecionado.value) {
            alert("Por favor, selecione um sabor antes de comprar.");
            return;
        }
        const input = document.createElement("input");
        input.type = "hidden"; input.name = "buyNow"; input.value = "true";
        addToCartForm.appendChild(input);
        addToCartForm.submit();
    });
});