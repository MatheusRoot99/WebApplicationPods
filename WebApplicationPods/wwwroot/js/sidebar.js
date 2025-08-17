document.addEventListener('DOMContentLoaded', function () {
    // Seleciona os elementos do sidebar
    const menuToggle = document.getElementById('menu-toggle');
    const wrapper = document.getElementById('wrapper');

    // Se não existir sidebar, apenas sai (sem erro no console)
    if (!menuToggle || !wrapper) {
        return;
    }

    // Função para alternar o estado do sidebar
    function toggleSidebar() {
        wrapper.classList.toggle('toggled');

        // Salva o estado no localStorage
        localStorage.setItem(
            'sidebarState',
            wrapper.classList.contains('toggled') ? 'collapsed' : 'expanded'
        );
    }

    // Adiciona o evento de clique
    menuToggle.addEventListener('click', function (e) {
        e.preventDefault();
        toggleSidebar();
    });

    // Verifica o estado salvo
    const sidebarState = localStorage.getItem('sidebarState');
    if (sidebarState === 'collapsed') {
        wrapper.classList.add('toggled');
    }

    // Ativa os tooltips do Bootstrap (somente se existir bootstrap)
    if (typeof bootstrap !== 'undefined') {
        const tooltipTriggerList = [].slice.call(
            document.querySelectorAll('[data-bs-toggle="tooltip"]')
        );
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
});
