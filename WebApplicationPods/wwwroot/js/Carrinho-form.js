document.addEventListener('DOMContentLoaded', function () {
    // Garante que o formulário está enviando os dados corretamente
    document.getElementById('addToCartForm')?.addEventListener('submit', function (e) {
        e.preventDefault();

        // Verifica se um sabor foi selecionado (se necessário)
        const saborSelecionado = document.getElementById('saborSelecionado').value;
        if (saborSelecionado === '' && document.querySelector('.flavor-radio:checked')) {
            alert('Por favor, selecione um sabor');
            return false;
        }

        this.submit();
    });

    // Atualiza o campo hidden do sabor quando um sabor é selecionado
    document.querySelectorAll('.flavor-radio').forEach(radio => {
        radio.addEventListener('change', function () {
            document.getElementById('saborSelecionado').value = this.value;
        });
    });

    document.addEventListener('DOMContentLoaded', function () {
        // Função para atualizar quantidade
        function updateQuantity(input, change) {
            let currentValue = parseInt(input.value) || 0;
            let newValue = currentValue + change;

            if (newValue < 1) newValue = 1;
            if (newValue > 999) newValue = 999;

            input.value = newValue;

            // Dispara evento de change para atualização automática se desejado
            const event = new Event('change');
            input.dispatchEvent(event);
        }

        // Event listeners para os botões de quantidade
        document.querySelectorAll('.quantidade-btn').forEach(btn => {
            btn.addEventListener('click', function () {
                const input = this.closest('.input-group').querySelector('.quantidade-input');
                const action = this.getAttribute('data-action');
                updateQuantity(input, action === 'increase' ? 1 : -1);
            });
        });

        // Atualização automática ao mudar valor (opcional)
        document.querySelectorAll('.quantidade-input').forEach(input => {
            input.addEventListener('change', function () {
                // Se quiser atualização automática ao mudar o valor, descomente:
                // this.closest('form').submit();
            });
        });
    });
});
