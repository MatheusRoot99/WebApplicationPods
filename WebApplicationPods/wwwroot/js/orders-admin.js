(function (w, d) {
    const NS = (w.__ORDERS_ADMIN__ = w.__ORDERS_ADMIN__ || {});

    const state = {
        opts: {},
        token: '',
        conn: null,
        lastHtml: '',
        refreshing: false,
        refreshQueued: false,
        lastRefreshAt: 0
    };

    function $(sel, root) {
        return (root || d).querySelector(sel);
    }

    function $all(sel, root) {
        return Array.from((root || d).querySelectorAll(sel));
    }

    function getToken() {
        if (state.token) return state.token;

        const meta = $('meta[name="request-verification-token"]');
        state.token = meta ? (meta.getAttribute('content') || '') : '';

        return state.token;
    }

    function esc(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    function showToast(message, ok = true) {
        const host = $('.toast-container');

        if (!host || !w.bootstrap || !bootstrap.Toast) {
            if (ok) {
                console.log(message);
            } else {
                alert(message);
            }

            return;
        }

        const id = 't' + Math.random().toString(16).slice(2);

        host.insertAdjacentHTML('beforeend', `
            <div id="${id}" class="toast align-items-center text-white ${ok ? 'bg-success' : 'bg-danger'} border-0 mb-2" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">${esc(message)}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Fechar"></button>
                </div>
            </div>
        `);

        const el = d.getElementById(id);
        const toast = new bootstrap.Toast(el, { delay: ok ? 2200 : 4200 });

        el.addEventListener('hidden.bs.toast', () => el.remove());
        toast.show();
    }

    function addSkeletonRows(tbody, count = 4) {
        const template = $('#row-skeleton');

        if (!template || !tbody) return;

        tbody.innerHTML = '';

        for (let i = 0; i < count; i++) {
            tbody.appendChild(template.content.cloneNode(true));
        }
    }

    function markLoading(tbody, on) {
        if (!tbody) return;
        tbody.classList.toggle('is-loading', !!on);
    }

    function applyFilter() {
        const tbody = $('#tbodyPedidos');
        const input = $('#qPedidos');
        const q = (input?.value || '').trim().toLowerCase();

        if (!tbody) return;

        const rows = $all('tr', tbody);

        if (!q) {
            rows.forEach(row => {
                row.style.display = '';
            });

            return;
        }

        rows.forEach(row => {
            const text = (row.innerText || '').toLowerCase();
            row.style.display = text.includes(q) ? '' : 'none';
        });
    }

    function highlightFromQueryString() {
        if (!state.opts.highlightFromQueryString) return;

        const params = new URLSearchParams(w.location.search);
        const id = params.get('highlight') || params.get('id');

        if (!id) return;
        if (!w.CSS || !CSS.escape) return;

        const row = d.querySelector(`tr[data-id="${CSS.escape(id)}"], tr[data-order-id="${CSS.escape(id)}"]`);

        if (!row) return;

        row.classList.add('row-highlight');
        row.scrollIntoView({ behavior: 'smooth', block: 'center' });

        setTimeout(() => row.classList.remove('row-highlight'), 2600);
    }

    async function readErrorMessage(response) {
        const contentType = response.headers.get('content-type') || '';

        try {
            if (contentType.includes('application/json')) {
                const data = await response.json();

                return data?.error ||
                    data?.message ||
                    data?.mensagem ||
                    'Não foi possível concluir a ação.';
            }

            const text = await response.text();

            if (text && text.length < 300) {
                return text;
            }
        } catch {
            return 'Não foi possível concluir a ação.';
        }

        return 'Não foi possível concluir a ação.';
    }

    async function postForm(form) {
        const url = form.getAttribute('action');
        const formData = new FormData(form);

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json'
            },
            body: formData,
            cache: 'no-store',
            credentials: 'same-origin',
            redirect: 'manual'
        });

        if (!response.ok && response.type !== 'opaqueredirect') {
            const message = await readErrorMessage(response);
            throw new Error(message);
        }

        return response;
    }

    function setButtonLoading(button, loading) {
        if (!button) return;

        if (loading) {
            if (!button.dataset.originalHtml) {
                button.dataset.originalHtml = button.innerHTML;
            }

            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Aguarde...';
            return;
        }

        button.disabled = false;

        if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
        }
    }

    function bindInlineActions() {
        const tbody = $('#tbodyPedidos');

        if (!tbody) return;

        $all('form.js-inline-action', tbody).forEach(form => {
            if (form.__bound) return;

            form.__bound = true;

            form.addEventListener('submit', async event => {
                event.preventDefault();

                const confirmMsg = (form.dataset.confirm || '').trim();

                if (confirmMsg && !confirm(confirmMsg)) return;

                const button = form.querySelector('button[type="submit"]');

                setButtonLoading(button, true);

                try {
                    await postForm(form);

                    showToast(form.dataset.success || 'Ação concluída.', true);
                    await NS.refreshTable(true);
                } catch (error) {
                    console.error(error);
                    showToast(error?.message || 'Não foi possível concluir a ação.', false);
                } finally {
                    setButtonLoading(button, false);
                }
            });
        });
    }

    NS.refreshTable = async function refreshTable(force = false) {
        const tbody = $('#tbodyPedidos');

        if (!tbody || !state.opts.tableUrl) return;

        const now = Date.now();

        if (!force && now - state.lastRefreshAt < 800) {
            state.refreshQueued = true;
            return;
        }

        if (state.refreshing) {
            state.refreshQueued = true;
            return;
        }

        state.refreshing = true;
        state.refreshQueued = false;
        state.lastRefreshAt = now;

        markLoading(tbody, true);
        addSkeletonRows(tbody, 4);

        try {
            const response = await fetch(state.opts.tableUrl, {
                cache: 'no-store',
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error('Falha ao carregar tabela.');
            }

            const html = await response.text();

            state.lastHtml = html || '';
            tbody.innerHTML = html || '';

            markLoading(tbody, false);
            bindInlineActions();
            applyFilter();
            highlightFromQueryString();
        } catch (error) {
            console.error(error);
            markLoading(tbody, false);
            showToast(error?.message || 'Falha ao atualizar a lista.', false);
        } finally {
            state.refreshing = false;

            if (state.refreshQueued) {
                state.refreshQueued = false;

                setTimeout(() => {
                    NS.refreshTable(true);
                }, 500);
            }
        }
    };

    function bindQuickSearch() {
        const input = $('#qPedidos');

        if (!input) return;

        input.addEventListener('input', applyFilter);
    }

    async function setupSignalR() {
        if (!w.signalR) {
            setInterval(() => NS.refreshTable(), 30000);
            return;
        }

        try {
            state.conn = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/pedidos')
                .withAutomaticReconnect()
                .build();

            const onAny = () => NS.refreshTable();

            state.conn.on('PedidosChanged', onAny);
            state.conn.on('NewOrder', onAny);
            state.conn.on('OrderUpdated', onAny);
            state.conn.on('OrderStatusChanged', onAny);

            await state.conn.start();

            console.log('[Orders] SignalR conectado');
        } catch (error) {
            console.warn('[Orders] SignalR não conectado:', error);
        }

        setInterval(() => NS.refreshTable(), 30000);
    }

    NS.boot = function boot(opts) {
        state.opts = opts || {};

        getToken();
        bindQuickSearch();
        bindInlineActions();
        setupSignalR();

        NS.refreshTable(true);
    };
})(window, document);