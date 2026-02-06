// wwwroot/js/sabores.js
// Contador de sabores selecionados e feedback visual (SEM jQuery, seguro em qualquer página)
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const container = document.querySelector('.produto-sabores-container');
        if (!container) return; // não existe na página? não faz nada

        // garante que só cria 1 contador (se a página recarregar via partial/hot reload)
        let counter = container.parentElement?.querySelector('.sabores-counter');
        if (!counter) {
            counter = document.createElement('div');
            counter.className = 'sabores-counter small text-muted mt-2';
            container.parentNode.insertBefore(counter, container.nextSibling);
        }

        function updateSaboresCounter() {
            const checkboxes = container.querySelectorAll('.produto-sabor-input');
            const selectedCount = Array.from(checkboxes).filter(cb => cb.checked).length;

            counter.textContent = `${selectedCount} sabor(es) selecionado(s)`;
            counter.style.color = selectedCount > 0 ? 'var(--bs-success)' : 'var(--bs-danger)';

            // feedback visual
            container.classList.toggle('has-selection', selectedCount > 0);
            container.classList.toggle('no-selection', selectedCount === 0);
        }

        // evento único no container (delegação)
        container.addEventListener('change', function (e) {
            const el = e.target;
            if (!(el instanceof HTMLInputElement)) return;
            if (!el.classList.contains('produto-sabor-input')) return;

            const label = el.nextElementSibling;
            if (label && label.classList.contains('produto-sabor-label')) {
                label.classList.toggle('active', el.checked);
            }

            updateSaboresCounter();
        });

        // inicial
        updateSaboresCounter();
    });
})();
