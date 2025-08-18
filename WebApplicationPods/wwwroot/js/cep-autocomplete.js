document.addEventListener('DOMContentLoaded', function () {
    // Para cada bloco de formulário que tenha data-cep-scope
    const scopes = document.querySelectorAll('[data-cep-scope]');

    scopes.forEach(scope => {
        const cepInput = scope.querySelector('[data-cep]');
        const logradouroInput = scope.querySelector('[data-field="logradouro"]');
        const bairroInput = scope.querySelector('[data-field="bairro"]');
        const cidadeInput = scope.querySelector('[data-field="cidade"]');
        const estadoInput = scope.querySelector('[data-field="estado"]');
        const complementoInput = scope.querySelector('[data-field="complemento"]');
        const numeroInput = scope.querySelector('[data-field="numero"]');

        if (!cepInput) return; // nada pra fazer neste scope

        // Máscara CEP
        cepInput.addEventListener('input', function (e) {
            let value = e.target.value.replace(/\D/g, '');
            if (value.length > 5) {
                value = value.substring(0, 5) + '-' + value.substring(5, 8);
            }
            e.target.value = value;
        });

        // Blur -> busca CEP
        cepInput.addEventListener('blur', async function () {
            const raw = cepInput.value.replace(/\D/g, '');
            if (raw.length !== 8) {
                feedback('CEP inválido. Deve conter 8 dígitos.', 'error');
                return;
            }
            loading(true);
            try {
                // ViaCEP
                const r = await fetch(`https://viacep.com.br/ws/${raw}/json/`);
                if (!r.ok) throw new Error('Erro na requisição');
                const data = await r.json();
                if (data.erro) throw new Error('CEP não encontrado');

                preencher({
                    logradouro: data.logradouro || '',
                    bairro: data.bairro || '',
                    cidade: data.localidade || '',
                    estado: data.uf || '',
                    complemento: data.complemento || ''
                });
                feedback('Endereço preenchido com sucesso!', 'success');
            } catch (err) {
                console.error('Erro ViaCEP:', err);
                feedback(err.message || 'Erro ao buscar CEP.', 'error');

                // Fallback BrasilAPI
                try {
                    const fb = await fetch(`https://brasilapi.com.br/api/cep/v2/${raw}`);
                    if (fb.ok) {
                        const j = await fb.json();
                        preencher({
                            logradouro: j.street || '',
                            bairro: j.neighborhood || '',
                            cidade: j.city || '',
                            estado: j.state || '',
                            complemento: j.complement || ''
                        });
                        feedback('Endereço preenchido via BrasilAPI', 'success');
                    }
                } catch (e2) {
                    console.error('Erro fallback:', e2);
                }
            } finally {
                loading(false);
            }
        });

        function preencher(obj) {
            if (logradouroInput) logradouroInput.value = obj.logradouro || '';
            if (bairroInput) bairroInput.value = obj.bairro || '';
            if (cidadeInput) cidadeInput.value = obj.cidade || '';
            if (estadoInput) estadoInput.value = obj.estado || '';
            if (complementoInput) complementoInput.value = obj.complemento || '';
            if (numeroInput) numeroInput.focus();
        }

        function loading(show) {
            const spinner = scope.querySelector('.loading-spinner');
            const cepGroup = scope.querySelector('.cep-group');
            if (spinner) spinner.style.opacity = show ? '1' : '0';
            if (cepGroup) show ? cepGroup.classList.add('loading') : cepGroup.classList.remove('loading');
        }

        function feedback(message, type) {
            // remove anterior apenas dentro do scope
            const old = scope.querySelector('.cep-feedback');
            if (old) old.remove();

            const div = document.createElement('div');
            div.className = `cep-feedback ${type}`;
            div.textContent = message;

            // Inserir logo após o input CEP deste scope
            cepInput.insertAdjacentElement('afterend', div);

            setTimeout(() => {
                div.style.opacity = '0';
                setTimeout(() => div.remove(), 300);
            }, 5000);
        }
    });
});
