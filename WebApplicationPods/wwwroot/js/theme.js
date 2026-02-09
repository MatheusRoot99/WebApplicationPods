(function () {
    const KEY = 'theme'; // 'light' | 'dark'

    function getPreferredTheme() {
        // 1) salvo pelo usuário
        const saved = localStorage.getItem(KEY);
        if (saved === 'light' || saved === 'dark') return saved;

        // 2) preferência do sistema
        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        return prefersDark ? 'dark' : 'light';
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        // Se quiser: atualiza ícone/texto dos botões
        document.querySelectorAll('[data-theme-toggle]').forEach(btn => {
            btn.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
            const icon = btn.querySelector('i');
            if (icon) {
                icon.classList.remove('fa-moon', 'fa-sun');
                icon.classList.add(theme === 'dark' ? 'fa-sun' : 'fa-moon');
            }
        });
    }

    function toggleTheme() {
        const current = document.documentElement.getAttribute('data-theme') || 'light';
        const next = current === 'dark' ? 'light' : 'dark';
        localStorage.setItem(KEY, next);
        applyTheme(next);
    }

    // aplica no load
    applyTheme(getPreferredTheme());

    // listeners em qualquer botão com data-theme-toggle
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-theme-toggle]');
        if (!btn) return;
        e.preventDefault();
        toggleTheme();
    });
})();
