document.addEventListener('DOMContentLoaded', function () {
    //setupQuantityControls();
   // setupRemoveForms();

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
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                }
            });

            // Verifica se a resposta é JSON
            const contentType = response.headers.get('content-type');
            if (!contentType || !contentType.includes('application/json')) {
                const errorText = await response.text();
                throw new Error(errorText || 'Resposta do servidor não é JSON');
            }

            const result = await response.json();

            if (!response.ok) {
                throw new Error(result.message || `Erro ${response.status}`);
            }

            if (result.success) {
                // Atualiza o subtotal da linha
                const produtoId = formData.get('produtoId');
                const sabor = formData.get('sabor') || '';

                // Encontra a linha correta (subtotal está na 4ª coluna)
                const row = document.querySelector(`tr.linha-item[data-produto-id="${produtoId}"][data-sabor="${sabor}"]`);
                if (row) {
                    const subtotalCell = row.querySelector('td:nth-child(4)');
                    if (subtotalCell) {
                        subtotalCell.textContent = result.subtotal;
                    }
                }

                // Atualiza o total do carrinho
                const totalElement = document.getElementById('totalCarrinho');
                if (totalElement) {
                    totalElement.textContent = result.total;
                }

                showAlert('success', result.message);
            } else {
                showAlert('danger', result.message);
                // Reverte o valor do input em caso de erro
                const input = form.querySelector('.quantidade-input');
                input.value = input.getAttribute('data-old-value') || input.value;
            }

        } catch (error) {
            console.error('Erro ao atualizar quantidade:', error);
            showAlert('danger', error.message || 'Erro ao atualizar quantidade');

            // Reverte o valor do input
            const input = form.querySelector('.quantidade-input');
            input.value = input.getAttribute('data-old-value') || input.value;
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
                // Salva o valor antigo antes de mudar
                input.setAttribute('data-old-value', input.value);
                input.value = newValue;
                updateQuantity(form, newValue);
            }
        });
    });

    // Configura o evento de mudança no input manual
    document.querySelectorAll('.quantidade-input').forEach(input => {
        // Salva o valor inicial
        input.setAttribute('data-old-value', input.value);

        input.addEventListener('change', function () {
            const form = this.closest('form.quantidade-form');
            if (!form) return;

            let value = parseInt(this.value) || 1;
            value = clampByMinMax(this, value);

            // Salva o valor atual antes de atualizar
            this.setAttribute('data-old-value', this.value);
            this.value = value;

            updateQuantity(form, value);
        });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.setAttribute('data-old-value', this.value);
                this.dispatchEvent(new Event('change'));
            }
        });

        // Salva o valor quando o usuário começa a editar
        input.addEventListener('focus', function () {
            this.setAttribute('data-old-value', this.value);
        });
    });
}

function setupRemoveForms() {
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
                        'X-Requested-With': 'XMLHttpRequest',
                        'Accept': 'application/json'
                    }
                });

                // Verifica se a resposta é JSON
                const contentType = response.headers.get('content-type');
                if (!contentType || !contentType.includes('application/json')) {
                    const errorText = await response.text();
                    throw new Error(errorText || 'Resposta do servidor não é JSON');
                }

                const result = await response.json();

                if (!response.ok) {
                    throw new Error(result.message || `Erro ${response.status}`);
                }

                if (result.success) {
                    // Remove a linha do item
                    const produtoId = formData.get('produtoId');
                    const sabor = formData.get('sabor') || '';
                    const row = document.querySelector(`tr.linha-item[data-produto-id="${produtoId}"][data-sabor="${sabor}"]`);

                    if (row) {
                        // Animação de fade out
                        row.style.transition = 'opacity 0.3s, transform 0.3s';
                        row.style.opacity = '0';
                        row.style.transform = 'translateX(-100px)';

                        setTimeout(() => {
                            row.remove();

                            // Atualiza o total do carrinho
                            const totalElement = document.getElementById('totalCarrinho');
                            if (totalElement) {
                                totalElement.textContent = result.total;
                            }

                            // Se não há mais itens, recarrega a página
                            if (document.querySelectorAll('.linha-item').length === 0) {
                                setTimeout(() => {
                                    location.reload();
                                }, 1000);
                            }
                        }, 300);
                    }

                    showAlert('success', result.message);

                } else {
                    showAlert('danger', result.message);
                }

            } catch (error) {
                console.error('Erro ao remover item:', error);
                showAlert('danger', error.message || 'Erro ao remover item');
            }
        });
    });
}

function showAlert(type, message) {
    const alertsContainer = document.getElementById('carrinho-alerts');
    if (!alertsContainer) {
        console.warn('Container de alerts não encontrado');
        return;
    }

    // Limpa alerts antigos do mesmo tipo para evitar duplicação
    const existingAlerts = alertsContainer.querySelectorAll(`.alert-${type}`);
    existingAlerts.forEach(alert => alert.remove());

    const alert = document.createElement('div');
    alert.className = `alert alert-${type} alert-dismissible fade show`;
    alert.innerHTML = `
        <i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle'}"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Fechar"></button>
    `;

    alertsContainer.appendChild(alert);

    // Auto-remove após 5 segundos
    setTimeout(() => {
        if (alert.parentNode) {
            alert.style.transition = 'opacity 0.5s';
            alert.style.opacity = '0';
            setTimeout(() => {
                if (alert.parentNode) {
                    alert.remove();
                }
            }, 500);
        }
    }, 5000);

    // Adiciona funcionalidade ao botão de fechar
    const closeBtn = alert.querySelector('.btn-close');
    if (closeBtn) {
        closeBtn.addEventListener('click', function () {
            alert.style.transition = 'opacity 0.5s';
            alert.style.opacity = '0';
            setTimeout(() => {
                if (alert.parentNode) {
                    alert.remove();
                }
            }, 500);
        });
    }
}

// Função auxiliar para debounce (evitar múltiplas chamadas rápidas)
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Adiciona suporte para Bootstrap 5 se necessário
if (typeof bootstrap !== 'undefined') {
    // Habilita tooltips se existirem
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Habilita popovers se existirem
    const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    popoverTriggerList.map(function (popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });
}