document.addEventListener('DOMContentLoaded', function () {
    // Seleciona os elementos
    const menuToggle = document.getElementById('menu-toggle');
    const wrapper = document.getElementById('wrapper');

    // Verifica se os elementos existem
    if (!menuToggle || !wrapper) {
        console.error('Elementos do sidebar não encontrados!');
        return;
    }

    // Função para alternar o estado do sidebar
    function toggleSidebar() {
        wrapper.classList.toggle('toggled');

        // Salva o estado no localStorage
        localStorage.setItem('sidebarState', wrapper.classList.contains('toggled') ? 'collapsed' : 'expanded');
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

    // Ativa os tooltips do Bootstrap
    if (typeof bootstrap !== 'undefined') {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
});