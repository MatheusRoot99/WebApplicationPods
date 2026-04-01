(function (w, d) {
    const NS = (w.__PANEL_NOTIFICATIONS__ = w.__PANEL_NOTIFICATIONS__ || {});

    const state = {
        dropdownUrl: '',
        countUrl: '',
        take: 8,
        returnUrl: '',
        conn: null,
        refreshing: false,
        poller: null
    };

    function $(sel, root) {
        return (root || d).querySelector(sel);
    }

    function getReturnUrl() {
        return state.returnUrl || (w.location.pathname + w.location.search);
    }

    function buildDropdownUrl() {
        if (!state.dropdownUrl) return '';

        const url = new URL(state.dropdownUrl, w.location.origin);

        if (state.take > 0) {
            url.searchParams.set('take', String(state.take));
        }

        const returnUrl = getReturnUrl();
        if (returnUrl) {
            url.searchParams.set('returnUrl', returnUrl);
        }

        return url.toString();
    }

    async function refreshDropdown() {
        const host = $('#panelNotificationsDropdownHost');
        const url = buildDropdownUrl();

        if (!host || !url) return;

        const res = await fetch(url, {
            cache: 'no-store',
            credentials: 'same-origin'
        });

        if (!res.ok) throw new Error('Falha ao atualizar dropdown de notificações.');

        host.innerHTML = await res.text();
    }

    async function refreshCount() {
        const badge = $('#panelNotificationsBadge');
        if (!badge || !state.countUrl) return;

        const res = await fetch(state.countUrl, {
            cache: 'no-store',
            credentials: 'same-origin'
        });

        if (!res.ok) throw new Error('Falha ao atualizar contador de notificações.');

        const data = await res.json();
        const count = Number(data?.count || 0);

        badge.textContent = String(count);
        badge.classList.toggle('d-none', count <= 0);
    }

    async function refreshAll() {
        if (state.refreshing) return;
        state.refreshing = true;

        try {
            await Promise.all([refreshDropdown(), refreshCount()]);
        } catch (e) {
            console.warn('[PanelNotifications] erro ao atualizar:', e);
        } finally {
            state.refreshing = false;
        }
    }

    function bindDropdownRefresh() {
        const toggle = $('#panelNotificationsToggle');
        const dropdown = toggle ? toggle.closest('.dropdown') : null;
        if (!dropdown || dropdown.__panelNotificationsBound) return;

        dropdown.__panelNotificationsBound = true;
        dropdown.addEventListener('show.bs.dropdown', refreshAll);
    }

    async function setupSignalR() {
        if (!w.signalR || state.conn) return;

        try {
            state.conn = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/pedidos')
                .withAutomaticReconnect()
                .build();

            const onChanged = () => refreshAll();

            state.conn.on('NotificacoesChanged', onChanged);
            state.conn.on('NewOrder', onChanged);
            state.conn.on('PedidosChanged', onChanged);
            state.conn.onreconnected(() => refreshAll());

            await state.conn.start();
            console.log('[PanelNotifications] SignalR conectado');
        } catch (e) {
            console.warn('[PanelNotifications] SignalR não conectou:', e);
        }
    }

    function startPolling() {
        if (state.poller) {
            w.clearInterval(state.poller);
        }

        state.poller = w.setInterval(refreshAll, 30000);
    }

    NS.boot = function boot(opts) {
        if (!$('#panelNotificationsDropdownHost') || !$('#panelNotificationsBadge'))
            return;

        state.dropdownUrl = opts?.dropdownUrl || '';
        state.countUrl = opts?.countUrl || '';
        state.take = Number(opts?.take || 8);
        state.returnUrl = opts?.returnUrl || '';

        bindDropdownRefresh();
        refreshAll();
        setupSignalR();
        startPolling();
    };
})(window, document);