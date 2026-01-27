// wwwroot/js/orders-admin.js
// Requisitos: Bootstrap (para classes), SignalR já referenciado na View
// Funções: intercepta submits .js-inline-action -> POST via fetch sem trocar de página;
//          mostra toasts; refreshTable(); busca rápida; highlight; skeleton enquanto carrega.

(function (w, d) {
    const mod = {
        opts: {},
        els: {},
        token: null,
        connection: null
    };

    function $(sel, root) { return (root || d).querySelector(sel); }
    function $all(sel, root) { return Array.from((root || d).querySelectorAll(sel)); }

    // Toast simples (Bootstrap-less)
    function showToast(msg, variant = 'success') {
        const cont = $('.toast-container');
        if (!cont) return alert(msg);
        const el = d.createElement('div');
        el.className = `toast align-items-center text-bg-${variant} border-0 show`;
        el.setAttribute('role', 'alert');
        el.style.minWidth = '260px';
        el.innerHTML = `
      <div class="d-flex">
        <div class="toast-body">${msg}</div>
        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
      </div>`;
        cont.appendChild(el);
        setTimeout(() => { el.remove(); }, 3500);
    }

    function getToken() {
        if (mod.token) return mod.token;
        const meta = $('meta[name="request-verification-token"]');
        mod.token = meta ? meta.getAttribute('content') : '';
        return mod.token;
    }

    async function postForm(form) {
        const url = form.getAttribute('action');
        const fd = new FormData(form);
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest'   // <--- ADICIONE ISTO
            },
            body: fd,
            redirect: 'manual'
        });
        return res;
    }


    async function refreshTable() {
        const tbody = $('#tbodyPedidos');
        if (!tbody) return;

        // skeleton rápido
        tbody.classList.add('is-loading');
        const tpl = $('#row-skeleton');
        if (tpl) {
            tbody.innerHTML = '';
            for (let i = 0; i < 3; i++) {
                tbody.appendChild(tpl.content.cloneNode(true));
            }
        }

        try {
            const res = await fetch(mod.opts.tableUrl, { cache: 'no-store' });
            const html = await res.text();
            tbody.innerHTML = html;
            tbody.classList.remove('is-loading');
            bindInlineActions(); // rebinda eventos nos novos elementos
            applyFilter();       // re-aplica filtro de busca rápida, se houver
            highlightFromQueryString();
        } catch (e) {
            tbody.classList.remove('is-loading');
            showToast('Falha ao atualizar a lista.', 'danger');
            console.error(e);
        }
    }

    // Busca rápida (cliente, método, status) — filtra no DOM
    function applyFilter() {
        const q = ($('#qPedidos')?.value || '').trim().toLowerCase();
        const rows = $all('#tbodyPedidos tr');
        if (!q) {
            rows.forEach(r => r.classList.remove('d-none'));
            return;
        }
        rows.forEach(r => {
            const text = r.innerText.toLowerCase();
            r.classList.toggle('d-none', !text.includes(q));
        });
    }

    function bindQuickSearch() {
        const inp = $('#qPedidos');
        if (!inp) return;
        inp.addEventListener('input', () => applyFilter());
    }

    function bindInlineActions() {
        const forms = $all('.js-inline-action', $('#tbodyPedidos'));
        forms.forEach(form => {
            form.addEventListener('submit', async (ev) => {
                ev.preventDefault();

                const confirmMsg = form.dataset.confirm;
                if (confirmMsg && !confirm(confirmMsg)) return;

                const btn = form.querySelector('button[type="submit"]');
                const prevHTML = btn ? btn.innerHTML : null;
                if (btn) {
                    btn.disabled = true;
                    btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span>Aguarde...`;
                }

                try {
                    const res = await postForm(form);
                    if (!res.ok && res.type !== 'opaqueredirect') throw new Error('Erro HTTP');

                    // Sucesso: feedback + refresh
                    const okMsg = form.dataset.success || 'Ação concluída.';
                    showToast(okMsg, 'success');
                    await refreshTable();
                } catch (e) {
                    console.error(e);
                    showToast('Não foi possível concluir a ação.', 'danger');
                } finally {
                    if (btn && prevHTML !== null) {
                        btn.disabled = false;
                        btn.innerHTML = prevHTML;
                    }
                }
            });
        });
    }

    // Destaque na linha via query ?highlight=ID
    function highlightFromQueryString() {
        if (!mod.opts.highlightFromQueryString) return;
        const params = new URLSearchParams(w.location.search);
        const id = params.get('highlight');
        if (!id) return;

        const row = d.querySelector(`tr[data-id="${id}"]`);
        if (row) {
            row.classList.add('row-highlight');
            row.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    // SignalR live refresh
    async function setupSignalR() {
        if (!w.signalR) return;
        try {
            mod.connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/pedidos")
                .withAutomaticReconnect()
                .build();

            mod.connection.on("PedidosChanged", () => refreshTable());
            await mod.connection.start();
        } catch (e) {
            console.warn('SignalR não conectado:', e);
        }

        // fallback: refresh a cada 30s
        setInterval(refreshTable, 30000);
    }

    function boot(opts) {
        mod.opts = opts || {};
        mod.els.root = $('#ordersRoot');
        if (!mod.els.root) return;

        // expõe para outros scripts, se necessário
        w.refreshTable = refreshTable;

        bindQuickSearch();
        bindInlineActions();
        setupSignalR();
    }

    w.__ORDERS_ADMIN__ = { boot, refreshTable };
})(window, document);
