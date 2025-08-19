document.addEventListener('DOMContentLoaded', function () {
    // Configura os eventos de quantidade
    setupQuantityControls();

    // Fade out para mensagens de alerta após 5 segundos
    setTimeout(() => {
        document.querySelectorAll('#carrinho-alerts .alert').forEach(alert => {
            alert.style.transition = 'opacity 0.5s';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 500);
        });
    }, 5000);
});

function setupQuantityControls() {
    // Função para garantir que o valor está dentro dos limites
    const clampByMinMax = (input, value) => {
        const min = parseInt(input.getAttribute('min')) || 1;
        const max = parseInt(input.getAttribute('max')) || 999;
        return Math.min(Math.max(value, min), max);
    };

    // Atualiza a quantidade via AJAX
    const updateQuantity = async (form, newValue) => {
        const formData = new FormData(form);
        formData.set('quantidade', newValue);

        // Adiciona o token anti-forgery se existir
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) throw new Error('Erro na requisição');

            const html = await response.text();
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, 'text/html');

            // Atualiza a linha do item específico
            const produtoId = formData.get('produtoId');
            const sabor = formData.get('sabor') || '';
            const updatedRow = doc.querySelector(`tr.linha-item[data-produto-id="${produtoId}"][data-sabor="${sabor}"]`);

            if (updatedRow) {
                const currentRow = document.querySelector(`tr.linha-item[data-produto-id="${produtoId}"][data-sabor="${sabor}"]`);
                if (currentRow) {
                    // Animação suave para destacar a mudança
                    currentRow.style.transition = 'background-color 0.3s';
                    currentRow.style.backgroundColor = 'rgba(0, 255, 0, 0.1)';

                    setTimeout(() => {
                        currentRow.innerHTML = updatedRow.innerHTML;
                        currentRow.style.backgroundColor = '';
                        // Reconfigura os eventos na linha atualizada
                        setupQuantityControls();
                    }, 300);
                }
            }

            // Atualiza o total do carrinho
            const updatedTotal = doc.getElementById('totalCarrinho');
            if (updatedTotal) {
                document.getElementById('totalCarrinho').innerHTML = updatedTotal.innerHTML;
            }

            // Mostra mensagens de sucesso/erro
            const alerts = doc.getElementById('carrinho-alerts');
            if (alerts && alerts.innerHTML.trim()) {
                const currentAlerts = document.getElementById('carrinho-alerts');
                currentAlerts.innerHTML = alerts.innerHTML;

                // Configura fade out para as novas mensagens
                setTimeout(() => {
                    currentAlerts.querySelectorAll('.alert').forEach(alert => {
                        alert.style.transition = 'opacity 0.5s';
                        alert.style.opacity = '0';
                        setTimeout(() => alert.remove(), 500);
                    });
                }, 5000);
            }

        } catch (error) {
            console.error('Erro ao atualizar quantidade:', error);
            // Mostra mensagem de erro temporária
            const errorMsg = document.createElement('div');
            errorMsg.className = 'alert alert-danger';
            errorMsg.innerHTML = '<i class="fas fa-exclamation-circle"></i> Erro ao atualizar quantidade';
            document.getElementById('carrinho-alerts').appendChild(errorMsg);
            setTimeout(() => errorMsg.remove(), 3000);
        }
    };

    // Configura os eventos nos botões de quantidade
    document.querySelectorAll('.quantidade-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const group = this.closest('.input-group');
            const input = group.querySelector('.quantidade-input');
            const form = this.closest('form.quantidade-form');

            if (!input || !form) return;

            const currentValue = parseInt(input.value) || 0;
            const action = this.getAttribute('data-action');
            const newValue = clampByMinMax(input,
                action === 'increase' ? currentValue + 1 : currentValue - 1);

            if (newValue !== currentValue) {
                input.value = newValue;
                updateQuantity(form, newValue);
            }
        });
    });

    // Configura o evento de mudança no input manual
    document.querySelectorAll('.quantidade-input').forEach(input => {
        input.addEventListener('change', function () {
            const form = this.closest('form.quantidade-form');
            if (!form) return;

            let value = parseInt(this.value) || 1;
            value = clampByMinMax(this, value);
            this.value = value;

            updateQuantity(form, value);
        });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.dispatchEvent(new Event('change'));
            }
        });
    });

    // Configura os forms de remoção para usar AJAX
    document.querySelectorAll('form[action*="RemoverItem"]').forEach(form => {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();

            const formData = new FormData(form);
            // Adiciona o token anti-forgery se existir
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                formData.append('__RequestVerificationToken', token.value);
            }

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) throw new Error('Erro na requisição');

                const html = await response.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');

                // Substitui todo o conteúdo do carrinho-container
                const updatedContent = doc.querySelector('.carrinho-container');
                if (updatedContent) {
                    document.querySelector('.carrinho-container').innerHTML = updatedContent.innerHTML;
                    // Reconfigura todos os eventos
                    setupQuantityControls();
                }

            } catch (error) {
                console.error('Erro ao remover item:', error);
                const errorMsg = document.createElement('div');
                errorMsg.className = 'alert alert-danger';
                errorMsg.innerHTML = '<i class="fas fa-exclamation-circle"></i> Erro ao remover item';
                document.getElementById('carrinho-alerts').appendChild(errorMsg);
                setTimeout(() => errorMsg.remove(), 3000);
            }
        });
    });
}