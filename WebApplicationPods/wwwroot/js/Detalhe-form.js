document.addEventListener("DOMContentLoaded", () => {
    /* ---------- dados do servidor ---------- */
    const productData = document.getElementById('product-data');
    let estoqueMaximo = parseInt(productData?.dataset.stock || "0");
    const initialDisabledState = productData?.dataset.isOutOfStock === 'true';
    const resumoUrl = productData?.dataset.resumoUrl || '/Carrinho/Resumo';

    /* ---------- tabs ---------- */
    const tabButtons = Array.from(document.querySelectorAll(".tab-button"));
    const panes = {
        description: document.getElementById("tab-description"),
        specifications: document.getElementById("tab-specifications"),
        reviews: document.getElementById("tab-reviews")
    };
    function activateTab(key) {
        tabButtons.forEach(btn => {
            const active = btn.dataset.tab === key;
            btn.classList.toggle("active", active);
            btn.setAttribute("aria-selected", String(active));
        });
        Object.entries(panes).forEach(([k, el]) => {
            const active = k === key;
            el.classList.toggle("active", active);
            el.hidden = !active;
        });
    }
    tabButtons.forEach(btn => btn.addEventListener("click", () => activateTab(btn.dataset.tab)));
    document.querySelector(".tabs-header")?.addEventListener("keydown", (e) => {
        const idx = tabButtons.findIndex(b => b.classList.contains("active"));
        if (["ArrowRight", "ArrowLeft"].includes(e.key)) {
            e.preventDefault();
            const next = e.key === "ArrowRight" ? (idx + 1) % tabButtons.length : (idx - 1 + tabButtons.length) % tabButtons.length;
            tabButtons[next].focus(); tabButtons[next].click();
        }
    });

    /* ---------- quantidade ---------- */
    const minusBtn = document.querySelector(".quantity-btn.minus");
    const plusBtn = document.querySelector(".quantity-btn.plus");
    const quantityInput = document.getElementById("quantity");
    const quantidadeHidden = document.getElementById("quantidadeHidden");
    const clamp = (v, min, max) => Math.min(Math.max(v, min), max);
    function syncQty(value) {
        const v = clamp(parseInt(value || 1), 1, Math.max(1, estoqueMaximo));
        if (quantityInput) quantityInput.value = v;
        if (quantidadeHidden) quantidadeHidden.value = v;
    }
    minusBtn?.addEventListener("click", () => syncQty((+quantityInput.value) - 1));
    plusBtn?.addEventListener("click", () => syncQty((+quantityInput.value) + 1));
    quantityInput?.addEventListener("change", (e) => syncQty(e.target.value));

    /* ---------- sabores & estoque ---------- */
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
            if (btn === addToCartBtn) {
                btn.innerHTML = disabled
                    ? '<i class="fas fa-shopping-cart" aria-hidden="true"></i> ESGOTADO'
                    : '<i class="fas fa-shopping-cart" aria-hidden="true"></i> ADICIONAR AO CARRINHO';
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
            if (estoqueInfo) estoqueInfo.hidden = true;
            if (saborSelecionado) saborSelecionado.value = "";
            estoqueMaximo = parseInt(productData?.dataset.stock || "0");
            syncQty(quantityInput.value);
            setBtnDisabled(initialDisabledState);
        }
    }

    // auto-seleciona primeiro com estoque
    const primeiroComEstoque = radios.find(r => !r.disabled && (parseInt(r.dataset.estoque) || 0) > 0);
    if (primeiroComEstoque) primeiroComEstoque.checked = true;
    radios.forEach(r => r.addEventListener("change", atualizarEstoquePorSelecao));
    atualizarEstoquePorSelecao();

    /* ---------- helpers ---------- */
    function showToastLocal(message, ok = true) {
        const toastEl = document.getElementById('appToast');
        const bodyEl = document.getElementById('appToastBody');
        if (!toastEl || !bodyEl) return;
        bodyEl.textContent = message;
        toastEl.classList.remove('bg-success', 'bg-danger');
        toastEl.classList.add(ok ? 'bg-success' : 'bg-danger');
        new bootstrap.Toast(toastEl, { delay: 2400 }).show();
    }
    function getAntiForgeryTokenFromForm(form) {
        return form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    /* ---------- ajax: adicionar ao carrinho ---------- */
    const addToCartForm = document.getElementById("addToCartForm");

    async function postAdicionarItem(bodyParams, token) {
        const url = addToCartForm?.getAttribute('action') || '/Carrinho/AdicionarItem';
        const body = new URLSearchParams();
        Object.entries(bodyParams).forEach(([k, v]) => { if (v !== undefined && v !== null) body.set(k, String(v)); });

        const resp = await fetch(url, {
            method: 'POST',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'RequestVerificationToken': token
            },
            body: body.toString(),
            credentials: 'same-origin'
        });
        if (!resp.ok) throw new Error('Falha na requisição');
        return await resp.json();
    }

    async function handleAddToCart(e, buyNowFlag) {
        e.preventDefault();

        if (radios.length > 0 && !saborSelecionado.value) {
            showToastLocal('Selecione um sabor antes de continuar.', false);
            return;
        }

        const v = parseInt(document.getElementById('quantity')?.value || '1', 10);
        const qtd = isNaN(v) || v < 1 ? 1 : v;
        if (quantidadeHidden) quantidadeHidden.value = String(qtd);

        const produtoId = addToCartForm?.querySelector('input[name="produtoId"]')?.value;
        const observacoes = document.getElementById('observacoesProduto')?.value || '';
        const token = getAntiForgeryTokenFromForm(addToCartForm);

        if (addToCartBtn) { addToCartBtn.disabled = true; addToCartBtn.classList.add('disabled'); }
        if (buyNowBtn && buyNowFlag) { buyNowBtn.disabled = true; buyNowBtn.classList.add('disabled'); }

        try {
            const result = await postAdicionarItem({
                produtoId, quantidade: qtd, sabor: saborSelecionado.value || '', observacoes, buyNow: !!buyNowFlag
            }, token);

            if (result.ok) {
                if (typeof result.count !== 'undefined' && window.updateCartBadges) window.updateCartBadges(result.count);
                if (window.cartMarkUnseen) window.cartMarkUnseen();
                showToastLocal(`${result.nome || 'Item'} adicionado ao carrinho!`, true);
                if (result.buyNow) window.location.href = resumoUrl;
            } else {
                showToastLocal(result.error || 'Não foi possível adicionar ao carrinho.', false);
            }
        } catch {
            showToastLocal('Erro de rede ao adicionar ao carrinho.', false);
        } finally {
            if (addToCartBtn) { addToCartBtn.disabled = false; addToCartBtn.classList.remove('disabled'); }
            if (buyNowBtn && buyNowFlag) { buyNowBtn.disabled = false; buyNowBtn.classList.remove('disabled'); }
        }
    }

    addToCartForm?.addEventListener("submit", (e) => handleAddToCart(e, false));
    document.querySelector(".btn-buy-now")?.addEventListener("click", (e) => handleAddToCart(e, true));

    /* ---------- sincroniza contador ao carregar ---------- */
    (async function () {
        try {
            const r = await fetch('/Carrinho/Count', { cache: 'no-store', credentials: 'same-origin' });
            if (!r.ok) return;
            const data = await r.json();
            if (typeof data.count !== 'undefined' && window.updateCartBadges) window.updateCartBadges(data.count);
        } catch { }
    })();
});
