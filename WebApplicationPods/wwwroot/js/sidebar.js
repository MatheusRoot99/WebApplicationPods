document.addEventListener('DOMContentLoaded', function () {
    // ===== Sidebar collapse (toggled) =====
    const menuToggle = document.getElementById('menu-toggle');
    const wrapper = document.getElementById('wrapper');

    if (menuToggle && wrapper) {
        function toggleSidebar() {
            wrapper.classList.toggle('toggled');
            try {
                localStorage.setItem(
                    'sidebarState',
                    wrapper.classList.contains('toggled') ? 'collapsed' : 'expanded'
                );
            } catch { }
        }

        menuToggle.addEventListener('click', function (e) {
            e.preventDefault();
            toggleSidebar();
        });

        try {
            const sidebarState = localStorage.getItem('sidebarState');
            if (sidebarState === 'collapsed') wrapper.classList.add('toggled');
        } catch { }
    }

    // ===== Tooltips =====
    if (typeof bootstrap !== 'undefined') {
        const tooltipTriggerList = [].slice.call(
            document.querySelectorAll('[data-bs-toggle="tooltip"]')
        );
        tooltipTriggerList.forEach(function (el) {
            try { new bootstrap.Tooltip(el); } catch { }
        });
    }

    // ===== Persistência de DROPDOWN/COLLAPSE do sidebar =====
    const sidebarRoot =
        document.getElementById('sidebar-wrapper') || document.querySelector('.sidebar');

    // Se não tem sidebar ou bootstrap, só ignora essa parte (sem parar o resto)
    if (!sidebarRoot || typeof bootstrap === 'undefined') {
        return;
    }

    const KEY_OPEN_COLLAPSES = 'sidebar_open_collapses';

    const readOpenSet = () => {
        try {
            const raw = localStorage.getItem(KEY_OPEN_COLLAPSES);
            const arr = raw ? JSON.parse(raw) : [];
            return new Set(Array.isArray(arr) ? arr : []);
        } catch {
            return new Set();
        }
    };

    const writeOpenSet = (set) => {
        try {
            localStorage.setItem(KEY_OPEN_COLLAPSES, JSON.stringify(Array.from(set)));
        } catch { }
    };

    const openSet = readOpenSet();

    // ✅ 0) Se o Razor já deixou algum collapse aberto (classe "show"),
    // salva isso no storage (assim não fecha ao navegar)
    sidebarRoot.querySelectorAll('.collapse[id].show').forEach((el) => {
        openSet.add(el.id);
    });
    writeOpenSet(openSet);

    // ✅ 1) Ao carregar: reabre os collapses salvos
    sidebarRoot.querySelectorAll('.collapse[id]').forEach((el) => {
        if (!openSet.has(el.id)) return;

        try {
            const inst = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            inst.show();
        } catch { }
    });

    // ✅ 2) Ao abrir/fechar: salva estado
    sidebarRoot.querySelectorAll('.collapse[id]').forEach((el) => {
        el.addEventListener('shown.bs.collapse', () => {
            openSet.add(el.id);
            writeOpenSet(openSet);
        });

        el.addEventListener('hidden.bs.collapse', () => {
            openSet.delete(el.id);
            writeOpenSet(openSet);
        });
    });
});
