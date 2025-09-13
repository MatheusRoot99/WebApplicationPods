// ==============================
// wwwroot/js/site.js
// ==============================

// ---------- Busca (opcional) ----------
document.getElementById('searchInput')?.addEventListener('keyup', function () {
    const term = (this.value || '').toLowerCase();
    document.querySelectorAll('.produto-card').forEach(card => {
        const title = card.querySelector('.card-title')?.textContent?.toLowerCase() || '';
        card.style.display = title.includes(term) ? 'block' : 'none';
    });
});

// ---------- Bootstrap Tooltips ----------
document.addEventListener('DOMContentLoaded', () => {
    const list = Array.from(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    list.forEach(el => new bootstrap.Tooltip(el));
});

// ==============================
// Helpers globais/fallbacks
// ==============================

// anti-forgery (usa o hidden do _Layout)
if (typeof window.getAntiForgeryToken !== 'function') {
    window.getAntiForgeryToken = function () {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    };
}

// toast do _Layout (reutiliza o existente)
function showToast(msg, ok = true) {
    const toastEl = document.getElementById('appToast');
    const bodyEl = document.getElementById('appToastBody');
    if (!toastEl || !bodyEl) return;
    bodyEl.textContent = msg;
    toastEl.classList.toggle('bg-success', ok);
    toastEl.classList.toggle('bg-danger', !ok);
    new bootstrap.Toast(toastEl, { delay: 2000 }).show();
}

// Se o layout ainda não definiu, cria um updateCartBadges simples
if (typeof window.updateCartBadges !== 'function') {
    window.updateCartBadges = function (count) {
        const n = Math.max(0, parseInt(count || 0, 10) || 0);
        ['cart-badge', 'cart-badge-mobile'].forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            if (n > 0) {
                el.textContent = String(n);
                el.classList.remove('visually-hidden');
                el.classList.add('mbn-badge-bump');
                setTimeout(() => el.classList.remove('mbn-badge-bump'), 400);
            } else {
                el.textContent = '0';
                el.classList.add('visually-hidden');
            }
        });
    };
}

// ==============================
// Interceptor universal do carrinho
// - Intercepta: AdicionarItem / AtualizarItem / RemoverItem
// - Usa ev.submitter para enviar op=inc/dec do botão clicado
// - Atualiza badges com o count vindo do servidor
// ==============================
// ==============================
// Intercepta Add / Update / Remove (AJAX)
// ==============================
(function () {
    let lastSubmitter = null;

    // fallback para navegadores sem ev.submitter
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('button[type="submit"],input[type="submit"]');
        if (btn) lastSubmitter = btn;
    }, true);

    async function postAjax(form, submitter) {
        const btn = submitter || form.querySelector('[type="submit"]');
        const old = btn ? btn.innerHTML : null;
        if (btn) { btn.disabled = true; btn.innerHTML = '<i class="fa fa-spinner fa-spin me-1"></i>Enviando...'; }

        try {
            const fd = (submitter && typeof FormData === 'function')
                ? new FormData(form, submitter) // inclui name/value do botão clicado (op=inc/dec)
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
                // se a action não retornou JSON, faz fallback para página
                if (res.redirected) { window.location.href = res.url; return; }
                window.location.reload(); return;
            }

            const data = await res.json(); // { ok, count, itemQty?, nome?, error? }
            if (!data?.ok) {
                showToast(data?.error || 'Operação não concluída.', false);
                return;
            }

            // atualiza badges
            if (typeof window.updateCartBadges === 'function') {
                window.updateCartBadges(data.count || 0);
            }

            // se atualizou quantidade, reflita no input
            if (data.itemQty != null) {
                const qtyInput = form.querySelector('input[name="quantidade"]');
                if (qtyInput) qtyInput.value = String(data.itemQty);
            }

            // se removeu, você pode remover a linha da tabela sem recarregar
            if (/\/RemoverItem(\b|\/|\?)/i.test(form.action)) {
                const row = form.closest('tr');
                if (row) row.remove();
            }

            // feedback
            if (/AdicionarItem/i.test(form.action)) showToast(`${data.nome || 'Produto'} adicionado ao carrinho!`, true);
            else if (/AtualizarItem/i.test(form.action)) showToast('Quantidade atualizada.', true);
            else if (/RemoverItem/i.test(form.action)) showToast('Item removido.', true);

        } catch (e) {
            console.error(e);
            showToast('Erro de rede.', false);
        } finally {
            if (btn) { btn.disabled = false; btn.innerHTML = old; }
            lastSubmitter = null;
        }
    }

    // Intercepta submits de Add/Update/Remove
    document.addEventListener('submit', function (ev) {
        const form = ev.target;
        if (!(form instanceof HTMLFormElement)) return;
        const action = form.getAttribute('action') || '';
        if (!/(\/AdicionarItem|\/AtualizarItem|\/RemoverItem)(\b|\/|\?)/i.test(action)) return;

        ev.preventDefault();
        const submitter = ev.submitter || lastSubmitter || form.querySelector('[type="submit"]');
        postAjax(form, submitter);
    }, true);
})();

