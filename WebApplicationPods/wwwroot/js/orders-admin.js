// wwwroot/js/orders-admin.js
(function (w, d) {
    const NS = (w.__ORDERS_ADMIN__ = w.__ORDERS_ADMIN__ || {});

    const state = {
        opts: {},
        token: '',
        conn: null,
        lastHtml: '',
        refreshing: false
    };

    function $(sel, root) { return (root || d).querySelector(sel); }
    function $all(sel, root) { return Array.from((root || d).querySelectorAll(sel)); }

    function getToken() {
        if (state.token) return state.token;
        const meta = $('meta[name="request-verification-token"]');
        state.token = meta ? (meta.getAttribute('content') || '') : '';
        return state.token;
    }

    function esc(s) {
        return String(s ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    function showToast(msg, ok = true) {
        const host = $('.toast-container');
        if (!host) return alert(msg);

        const id = 't' + Math.random().toString(16).slice(2);
        host.insertAdjacentHTML('beforeend', `
      <div id="${id}" class="toast align-items-center text-white ${ok ? 'bg-success' : 'bg-danger'} border-0 mb-2" role="alert" aria-live="assertive" aria-atomic="true">
        <div class="d-flex">
          <div class="toast-body">${esc(msg)}</div>
          <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Fechar"></button>
        </div>
      </div>
    `);

        const el = d.getElementById(id);
        const t = new bootstrap.Toast(el, { delay: 2200 });
        el.addEventListener('hidden.bs.toast', () => el.remove());
        t.show();
    }

    function addSkeletonRows(tbody, n = 4) {
        const tpl = $('#row-skeleton');
        if (!tpl || !tbody) return;
        tbody.innerHTML = '';
        for (let i = 0; i < n; i++) tbody.appendChild(tpl.content.cloneNode(true));
    }

    function markLoading(tbody, on) {
        if (!tbody) return;
        tbody.classList.toggle('is-loading', !!on);
    }

    function applyFilter() {
        const tbody = $('#tbodyPedidos');
        const q = ($('#qPedidos')?.value || '').trim().toLowerCase();
        if (!tbody) return;

        const rows = $all('tr', tbody);
        if (!q) {
            rows.forEach(r => (r.style.display = ''));
            return;
        }

        rows.forEach(r => {
            const text = (r.innerText || '').toLowerCase();
            r.style.display = text.includes(q) ? '' : 'none';
        });
    }

    function highlightFromQueryString() {
        if (!state.opts.highlightFromQueryString) return;
        const params = new URLSearchParams(w.location.search);
        const id = params.get('highlight') || params.get('id');
        if (!id) return;

        const row = d.querySelector(`tr[data-id="${CSS.escape(id)}"], tr[data-order-id="${CSS.escape(id)}"]`);
        if (row) {
            row.classList.add('row-highlight');
            row.scrollIntoView({ behavior: 'smooth', block: 'center' });
            setTimeout(() => row.classList.remove('row-highlight'), 2600);
        }
    }

    async function postForm(form) {
        const url = form.getAttribute('action');
        const fd = new FormData(form);

        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: fd,
            cache: 'no-store',
            credentials: 'same-origin',
            redirect: 'manual'
        });

        return res;
    }

    function bindInlineActions() {
        const tbody = $('#tbodyPedidos');
        if (!tbody) return;

        $all('form.js-inline-action', tbody).forEach(form => {
            if (form.__bound) return;
            form.__bound = true;

            form.addEventListener('submit', async (ev) => {
                ev.preventDefault();

                const confirmMsg = (form.dataset.confirm || '').trim();
                if (confirmMsg && !confirm(confirmMsg)) return;

                const btn = form.querySelector('button[type="submit"]');
                const prevHTML = btn ? btn.innerHTML : null;

                if (btn) {
                    btn.disabled = true;
                    btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span>Aguarde...`;
                }

                try {
                    const res = await postForm(form);

                    if (!res.ok && res.type !== 'opaqueredirect') {
                        throw new Error(`HTTP ${res.status}`);
                    }

                    showToast(form.dataset.success || 'Ação concluída.', true);
                    await NS.refreshTable();
                } catch (e) {
                    console.error(e);
                    showToast('Não foi possível concluir a ação.', false);
                } finally {
                    if (btn && prevHTML !== null) {
                        btn.disabled = false;
                        btn.innerHTML = prevHTML;
                    }
                }
            });
        });
    }

    NS.refreshTable = async function refreshTable() {
        const tbody = $('#tbodyPedidos');
        if (!tbody || !state.opts.tableUrl || state.refreshing) return;

        state.refreshing = true;
        markLoading(tbody, true);
        addSkeletonRows(tbody, 4);

        try {
            const res = await fetch(state.opts.tableUrl, { cache: 'no-store', credentials: 'same-origin' });
            if (!res.ok) throw new Error('Falha ao carregar tabela');

            const html = await res.text();

            // evita piscar se veio igual
            if (html && html !== state.lastHtml) {
                state.lastHtml = html;
                tbody.innerHTML = html;
            } else {
                tbody.innerHTML = html; // mantém ok (algumas vezes precisa)
            }

            markLoading(tbody, false);
            bindInlineActions();
            applyFilter();
            highlightFromQueryString();
        } catch (e) {
            console.error(e);
            markLoading(tbody, false);
            showToast('Falha ao atualizar a lista.', false);
        } finally {
            state.refreshing = false;
        }
    };

    function bindQuickSearch() {
        const inp = $('#qPedidos');
        if (!inp) return;
        inp.addEventListener('input', applyFilter);
    }

    async function setupSignalR() {
        if (!w.signalR) {
            // fallback polling
            setInterval(() => NS.refreshTable(), 30000);
            return;
        }

        try {
            state.conn = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/pedidos')
                .withAutomaticReconnect()
                .build();

            const onAny = () => NS.refreshTable();

            // aceita vários nomes (não quebra se seu hub usa outro)
            state.conn.on('PedidosChanged', onAny);
            state.conn.on('NewOrder', onAny);
            state.conn.on('OrderUpdated', onAny);
            state.conn.on('OrderStatusChanged', onAny);

            await state.conn.start();
            console.log('[Orders] SignalR conectado');
        } catch (e) {
            console.warn('[Orders] SignalR não conectado:', e);
        }

        // fallback polling
        setInterval(() => NS.refreshTable(), 30000);
    }

    NS.boot = function boot(opts) {
        state.opts = opts || {};
        getToken();

        bindQuickSearch();
        bindInlineActions();
        setupSignalR();

        // primeira carga
        NS.refreshTable();
    };
})(window, document);
