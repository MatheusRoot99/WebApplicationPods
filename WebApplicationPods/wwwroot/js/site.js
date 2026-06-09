document.getElementById('searchInput')?.addEventListener('keyup', function () {
    const term = (this.value || '').toLowerCase();

    document.querySelectorAll('.produto-card').forEach(card => {
        const title = card.querySelector('.card-title')?.textContent?.toLowerCase() || '';
        card.style.display = title.includes(term) ? 'block' : 'none';
    });
});

document.addEventListener('DOMContentLoaded', () => {
    const list = Array.from(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    list.forEach(el => new bootstrap.Tooltip(el));
});

if (typeof window.getAntiForgeryToken !== 'function') {
    window.getAntiForgeryToken = function () {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    };
}

function showToast(msg, ok = true) {
    const toastEl = document.getElementById('appToast');
    const bodyEl = document.getElementById('appToastBody');

    if (!toastEl || !bodyEl) {
        console.log(msg);
        return;
    }

    bodyEl.textContent = msg;
    toastEl.classList.toggle('bg-success', ok);
    toastEl.classList.toggle('bg-danger', !ok);
    new bootstrap.Toast(toastEl, { delay: 2000 }).show();
}

window.updateCartBadges = function (count) {
    const n = Math.max(0, parseInt(count || 0, 10) || 0);

    try {
        localStorage.setItem('cart_count', String(n));
    } catch { }

    document.querySelectorAll('[data-cart-count], .cart-count, #cartCount, .js-cart-badge, #cart-badge-mobile-bottom').forEach(el => {
        if (n > 0) {
            el.textContent = String(n);
            el.classList.add('show');
            el.classList.remove('visually-hidden');
            el.setAttribute('aria-label', `${n} item(ns) no carrinho`);
        } else {
            el.textContent = '';
            el.classList.remove('show');
            el.classList.add('visually-hidden');
            el.removeAttribute('aria-label');
        }
    });

    const dock = document.getElementById('storeCartDock');
    const dockCount = document.getElementById('storeCartDockCount');
    const dockText = document.getElementById('storeCartDockText');

    if (dockCount) dockCount.textContent = String(n);
    if (dockText) dockText.textContent = String(n);
    if (dock) dock.classList.toggle('is-visible', n > 0);
};

(function () {
    let lastSubmitter = null;

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('button[type="submit"],input[type="submit"]');
        if (btn) lastSubmitter = btn;
    }, true);

    async function postAjax(form, submitter) {
        const btn = submitter || form.querySelector('[type="submit"]');
        const old = btn ? btn.innerHTML : null;

        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<i class="fa fa-spinner fa-spin me-1"></i>Enviando...';
        }

        try {
            const fd = submitter && typeof FormData === 'function'
                ? new FormData(form, submitter)
                : new FormData(form);

            const res = await fetch(form.action, {
                method: 'POST',
                body: fd,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': (window.getAntiForgeryToken && window.getAntiForgeryToken()) || ''
                },
                credentials: 'same-origin',
                cache: 'no-store'
            });

            const ct = res.headers.get('content-type') || '';

            if (!ct.includes('application/json')) {
                if (res.redirected) {
                    window.location.href = res.url;
                    return;
                }

                window.location.reload();
                return;
            }

            const data = await res.json();

            if (!data?.ok) {
                showToast(data?.error || 'Operação não concluída.', false);
                return;
            }

            if (typeof window.updateCartBadges === 'function') {
                window.updateCartBadges(data.count || 0);
            }

            if (data.itemQty != null) {
                const qtyInput = form.querySelector('input[name="quantidade"]');
                if (qtyInput) qtyInput.value = String(data.itemQty);
            }

            if (/\/RemoverItem(\b|\/|\?)/i.test(form.action)) {
                const row = form.closest('tr');
                if (row) row.remove();
            }

            if (data.buyNow) {
                window.location.href = '/Carrinho/Resumo';
                return;
            }

            if (/AdicionarItem/i.test(form.action)) showToast(`${data.nome || 'Produto'} adicionado ao carrinho!`, true);
            else if (/AtualizarItem/i.test(form.action)) showToast('Quantidade atualizada.', true);
            else if (/RemoverItem/i.test(form.action)) showToast('Item removido.', true);

        } catch (e) {
            console.error(e);
            showToast('Erro de rede.', false);
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = old;
            }

            lastSubmitter = null;
        }
    }

    document.addEventListener('submit', function (ev) {
        const form = ev.target;

        if (!(form instanceof HTMLFormElement)) return;
        if (form.hasAttribute('data-cart-local-handler')) return;

        const action = form.getAttribute('action') || '';

        if (!/(\/AdicionarItem|\/AtualizarItem|\/RemoverItem)(\b|\/|\?)/i.test(action)) return;

        ev.preventDefault();

        const submitter = ev.submitter || lastSubmitter || form.querySelector('[type="submit"]');
        postAjax(form, submitter);
    }, true);
})();

async function refreshCartBadge() {
    try {
        const r = await fetch('/Carrinho/Count', { cache: 'no-store', credentials: 'same-origin' });
        const data = await r.json();

        if (typeof window.updateCartBadges === 'function') {
            window.updateCartBadges(data.count || 0);
        }
    } catch { }
}
