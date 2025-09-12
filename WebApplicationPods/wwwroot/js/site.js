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

// token anti-forgery (usa função do _Layout se existir)
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
    new bootstrap.Toast(toastEl, { delay: 2200 }).show();
}

// ==============================
// Interceptor universal do carrinho
// - Qualquer <form asp-action="AdicionarItem"> vira AJAX
// - Atualiza badges, marca dot e mostra toast
// ==============================
(function () {
    async function addToCartAjax(form) {
        const btn = form.querySelector('[type="submit"]');
        const old = btn ? btn.innerHTML : null;
        if (btn) { btn.disabled = true; btn.innerHTML = '<i class="fa fa-spinner fa-spin me-1"></i>Adicionando...'; }

        try {
            const fd = new FormData(form);
            const res = await fetch(form.action, {
                method: 'POST',
                body: fd,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': window.getAntiForgeryToken()
                },
                credentials: 'same-origin',
                cache: 'no-store'
            });

            const ct = res.headers.get('content-type') || '';
            if (!ct.includes('application/json')) {
                if (res.redirected) { window.location.href = res.url; return; }
                window.location.reload(); return;
            }

            const data = await res.json(); // { ok, count, nome, error? }
            if (data?.ok) {
                // mantém o “dot” até finalizar/zerar
                if (typeof window.cartMarkUnseen === 'function') window.cartMarkUnseen();

                // atualiza badges + persiste contagem
                if (typeof window.updateCartBadges === 'function') window.updateCartBadges(data.count || 0);

                showToast(`${data.nome || 'Produto'} adicionado ao carrinho!`, true);
            } else {
                showToast(data?.error || 'Não foi possível adicionar ao carrinho.', false);
            }
        } catch (e) {
            console.error(e);
            showToast('Erro de rede ao adicionar ao carrinho.', false);
        } finally {
            if (btn) { btn.disabled = false; btn.innerHTML = old; }
        }
    }

    // Intercepta qualquer submit cujo action contenha "/AdicionarItem"
    document.addEventListener('submit', function (ev) {
        const form = ev.target;
        if (!(form instanceof HTMLFormElement)) return;
        const action = form.getAttribute('action') || '';
        if (!/\/AdicionarItem(\b|\/|\?)/i.test(action)) return;

        ev.preventDefault();
        addToCartAjax(form);
    }, true);
})();
