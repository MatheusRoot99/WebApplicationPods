// Contador de sabores selecionados e feedback visual
document.addEventListener('DOMContentLoaded', function () {
    const container = document.querySelector('.produto-sabores-container');
    if (!container) return;

    // Cria o elemento do contador
    const counter = document.createElement('div');
    counter.className = 'sabores-counter small text-muted mt-2';
    container.parentNode.insertBefore(counter, container.nextSibling);

    // Atualiza o contador e estilos
    function updateSaboresCounter() {
        const checkboxes = container.querySelectorAll('.produto-sabor-input');
        const selectedCount = Array.from(checkboxes).filter(cb => cb.checked).length;

        // Atualiza o contador
        counter.textContent = `${selectedCount} sabor(es) selecionado(s)`;
        counter.style.color = selectedCount > 0 ? 'var(--bs-success)' : 'var(--bs-danger)';

        // Adiciona classe ao container para feedback visual
        container.classList.toggle('has-selection', selectedCount > 0);
    }

    // Configura os event listeners
    container.addEventListener('change', function (e) {
        if (e.target.classList.contains('produto-sabor-input')) {
            // Atualiza o estilo do label
            const label = e.target.nextElementSibling;
            if (label && label.classList.contains('produto-sabor-label')) {
                label.classList.toggle('active', e.target.checked);
            }
            updateSaboresCounter();
        }
    });

    // Inicializa
    updateSaboresCounter();
});

// Controle de sabores selecionados
$(document).ready(function () {
    // Atualiza visualização dos sabores selecionados
    $('.produto-sabor-input').change(function () {
        $(this).next('.produto-sabor-label').toggleClass('active', this.checked);
        updateSaboresCounter();
    });

    // Contador de sabores selecionados
    function updateSaboresCounter() {
        const selectedCount = $('.produto-sabor-input:checked').length;
        $('.sabores-counter').text(`${selectedCount} sabor(es) selecionado(s)`);

        // Adiciona/remove classe de erro se nenhum selecionado
        const container = $('.produto-sabores-container');
        container.toggleClass('no-selection', selectedCount === 0);
    }

    // Inicializa
    updateSaboresCounter();
});