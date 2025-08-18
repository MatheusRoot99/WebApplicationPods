document.addEventListener('DOMContentLoaded', function () {
    const cepInput = document.getElementById('cep');
    const logradouroInput = document.getElementById('logradouro');
    const bairroInput = document.getElementById('bairro');
    const cidadeInput = document.getElementById('cidade');
    const estadoInput = document.getElementById('estado');
    const complementoInput = document.getElementById('complemento');
    const numeroInput = document.getElementById('numero');

    // Adiciona máscara ao CEP
    if (cepInput) {
        cepInput.addEventListener('input', function (e) {
            let value = e.target.value.replace(/\D/g, '');
            if (value.length > 5) {
                value = value.substring(0, 5) + '-' + value.substring(5, 8);
            }
            e.target.value = value;
        });

        cepInput.addEventListener('blur', buscarCEP);
    }

    async function buscarCEP() {
        const cep = cepInput.value.replace(/\D/g, '');

        if (cep.length !== 8) {
            mostrarFeedback('CEP inválido. Deve conter 8 dígitos.', 'error');
            return;
        }

        mostrarLoading(true);

        try {
            // Tentativa com ViaCEP (fallback para outras APIs)
            const response = await fetch(`https://viacep.com.br/ws/${cep}/json/`);

            if (!response.ok) throw new Error('Erro na requisição');

            const data = await response.json();

            if (data.erro) {
                throw new Error('CEP não encontrado');
            }

            preencherCamposEndereco(data);
            mostrarFeedback('Endereço preenchido com sucesso!', 'success');
        } catch (error) {
            console.error('Erro ao buscar CEP:', error);
            mostrarFeedback(error.message || 'Erro ao buscar CEP. Tente novamente.', 'error');

            // Fallback para outra API caso a primeira falhe
            try {
                const fallbackResponse = await fetch(`https://brasilapi.com.br/api/cep/v2/${cep}`);
                if (fallbackResponse.ok) {
                    const fallbackData = await fallbackResponse.json();
                    preencherCamposEndereco({
                        logradouro: fallbackData.street || '',
                        bairro: fallbackData.neighborhood || '',
                        localidade: fallbackData.city || '',
                        uf: fallbackData.state || '',
                        complemento: fallbackData.complement || ''
                    });
                    mostrarFeedback('Endereço preenchido via BrasilAPI', 'success');
                }
            } catch (fallbackError) {
                console.error('Erro no fallback:', fallbackError);
            }
        } finally {
            mostrarLoading(false);
        }
    }

    function preencherCamposEndereco(data) {
        logradouroInput.value = data.logradouro || '';
        bairroInput.value = data.bairro || '';
        cidadeInput.value = data.localidade || '';
        estadoInput.value = data.uf || '';
        complementoInput.value = data.complemento || '';

        // Foca no campo número após preenchimento
        if (numeroInput) {
            numeroInput.focus();
        }
    }

    function mostrarLoading(show) {
        const loadingElement = document.querySelector('.loading-spinner');
        const cepGroup = document.querySelector('.cep-group');

        if (loadingElement) {
            loadingElement.style.opacity = show ? '1' : '0';
        }

        if (cepGroup) {
            show ? cepGroup.classList.add('loading') : cepGroup.classList.remove('loading');
        }
    }

    function mostrarFeedback(message, type) {
        // Remove feedbacks anteriores
        const oldFeedback = document.querySelector('.cep-feedback');
        if (oldFeedback) {
            oldFeedback.remove();
        }

        // Cria novo elemento de feedback
        const feedback = document.createElement('div');
        feedback.className = `cep-feedback ${type}`;
        feedback.textContent = message;

        // Insere após o campo CEP
        if (cepInput) {
            cepInput.insertAdjacentElement('afterend', feedback);
        }

        // Remove após 5 segundos
        setTimeout(() => {
            feedback.style.opacity = '0';
            setTimeout(() => feedback.remove(), 300);
        }, 5000);
    }
});