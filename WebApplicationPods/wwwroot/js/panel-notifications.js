(function (w, d) {
    const NS = (w.__PANEL_NOTIFICATIONS__ = w.__PANEL_NOTIFICATIONS__ || {});

    const state = {
        dropdownUrl: '',
        countUrl: '',
        conn: null,
        refreshing: false
    };

    function $(sel, root) {
        return (root || d).querySelector(sel);
    }

    async function refreshDropdown() {
        const host = $('#panelNotificationsDropdownHost');
        if (!host || !state.dropdownUrl) return;

        const res = await fetch(state.dropdownUrl, {
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
        if (!dropdown) return;

        dropdown.addEventListener('show.bs.dropdown', refreshAll);
    }

    async function setupSignalR() {
        if (!w.signalR) return;

        try {
            state.conn = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/pedidos')
                .withAutomaticReconnect()
                .build();

            const onChanged = () => refreshAll();

            state.conn.on('NotificacoesChanged', onChanged);
            state.conn.on('NewOrder', onChanged);
            state.conn.on('PedidosChanged', onChanged);

            await state.conn.start();
            console.log('[PanelNotifications] SignalR conectado');
        } catch (e) {
            console.warn('[PanelNotifications] SignalR não conectou:', e);
        }
    }

    NS.boot = function boot(opts) {
        if (!$('#panelNotificationsDropdownHost') || !$('#panelNotificationsBadge'))
            return;

        state.dropdownUrl = opts?.dropdownUrl || '';
        state.countUrl = opts?.countUrl || '';

        bindDropdownRefresh();
        refreshAll();
        setupSignalR();

        setInterval(refreshAll, 30000);
    };
})(window, document);