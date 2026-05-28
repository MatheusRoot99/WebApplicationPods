(function () {
    const buttons = Array.from(document.querySelectorAll('[data-theme-option]'));

    function getTheme() {
        return document.documentElement.getAttribute('data-theme') || 'light';
    }

    function setTheme(theme) {
        const next = theme === 'dark' ? 'dark' : 'light';

        document.documentElement.setAttribute('data-theme', next);

        try {
            localStorage.setItem('theme', next);
        } catch { }

        syncButtons();
    }

    function syncButtons() {
        const current = getTheme();

        buttons.forEach(button => {
            const theme = button.getAttribute('data-theme-option');
            button.classList.toggle('is-active', theme === current);
        });
    }

    buttons.forEach(button => {
        button.addEventListener('click', function () {
            setTheme(button.getAttribute('data-theme-option'));
        });
    });

    syncButtons();
})();