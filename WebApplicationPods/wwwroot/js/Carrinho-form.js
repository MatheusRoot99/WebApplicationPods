document.addEventListener('DOMContentLoaded', function () {
    // ================================
    // FORMULÁRIO DE ADIÇÃO AO CARRINHO
    // ================================
    const addToCartForm = document.getElementById('addToCartForm');
    if (addToCartForm) {
        addToCartForm.addEventListener('submit', function (e) {
            e.preventDefault();

            const saborInput = document.getElementById('saborSelecionado');
            const saborSelecionado = saborInput ? saborInput.value : '';
            const radioSelecionado = document.querySelector('.flavor-radio:checked');

            // Verifica se é obrigatório escolher sabor
            if (saborInput && radioSelecionado && saborSelecionado === '') {
                alert('Por favor, selecione um sabor');
                return false;
            }

            this.submit();
        });
    }

    // Atualiza o campo hidden com o sabor escolhido
    document.querySelectorAll('.flavor-radio').forEach(radio => {
        radio.addEventListener('change', function () {
            const saborInput = document.getElementById('saborSelecionado');
            if (saborInput) {
                saborInput.value = this.value;
            }
        });
    });

    // ================================
    // CONTROLE DE QUANTIDADE
    // ================================
    function updateQuantity(input, change) {
        let currentValue = parseInt(input.value) || 0;
        let newValue = currentValue + change;

        if (newValue < 1) newValue = 1;
        if (newValue > 999) newValue = 999;

        input.value = newValue;

        // Dispara evento de change (caso precise atualizar algo no front)
        input.dispatchEvent(new Event('change'));
    }

    // Botões + e -
    document.querySelectorAll('.quantidade-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const input = this.closest('.input-group')?.querySelector('.quantidade-input');
            if (input) {
                const action = this.getAttribute('data-action');
                updateQuantity(input, action === 'increase' ? 1 : -1);
            }
        });
    });

    // Atualização ao editar manualmente
    document.querySelectorAll('.quantidade-input').forEach(input => {
        input.addEventListener('change', function () {
            let value = parseInt(this.value) || 1;
            if (value < 1) value = 1;
            if (value > 999) value = 999;
            this.value = value;
        });
    });
});
