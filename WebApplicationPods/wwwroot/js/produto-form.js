// produto-form.js
document.addEventListener('DOMContentLoaded', function () {
    // Elementos principais
    const varsBody = document.getElementById('varsBody');
    const btnAddVar = document.getElementById('btnAddVar');
    const estoqueTotalInput = document.getElementById('EstoqueTotalCalc');
    const estoqueTotalBadge = document.getElementById('estoqueTotalBadge');

    if (!varsBody || !btnAddVar) {
        console.error('Elementos necessários não encontrados');
        return;
    }

    // ============== FUNÇÕES UTILITÁRIAS ==============

    function toInt(valor) {
        if (valor === null || valor === undefined || valor === '') return 0;
        const numero = parseInt(String(valor).replace(/\D/g, ''), 10);
        return isNaN(numero) ? 0 : numero;
    }

    function parseMoneyBR(valor) {
        if (!valor || valor === '') return 0;

        let stringValor = String(valor).trim();
        stringValor = stringValor.replace(/[^\d,-]/g, '');

        if (stringValor.includes(',') && stringValor.includes('.')) {
            stringValor = stringValor.replace(/\./g, '');
            stringValor = stringValor.replace(',', '.');
        } else if (stringValor.includes(',')) {
            stringValor = stringValor.replace(',', '.');
        }

        const numero = parseFloat(stringValor);
        return isNaN(numero) ? 0 : numero;
    }

    function formatMoneyBR(valor, incluirZeroDecimal = false) {
        const numero = typeof valor === 'number' ? valor : parseMoneyBR(valor);
        if (isNaN(numero)) return incluirZeroDecimal ? '0,00' : '';
        if (numero === 0) return incluirZeroDecimal ? '0,00' : '';

        return numero.toLocaleString('pt-BR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function formatMoneyInput(element) {
        let valor = element.value;
        valor = valor.replace(/\D/g, '');

        if (valor === '') {
            element.value = '';
            return;
        }

        const numero = parseInt(valor) / 100;
        element.value = formatMoneyBR(numero, true);
    }

    function renumberRows() {
        const rows = Array.from(varsBody.querySelectorAll('.var-row'));

        rows.forEach((row, index) => {
            row.dataset.index = index;

            row.querySelectorAll('[name]').forEach(input => {
                const oldName = input.getAttribute('name');
                if (oldName) {
                    const newName = oldName.replace(
                        /Variacoes\[\d+\]/g,
                        `Variacoes[${index}]`
                    );
                    input.setAttribute('name', newName);
                }
            });
        });
    }

    function calcularEstoqueTotal() {
        let total = 0;

        varsBody.querySelectorAll('.var-row').forEach(row => {
            const ativo = row.querySelector('.var-ativo').checked;
            if (!ativo) return;

            const estoque = toInt(row.querySelector('.var-estoque').value);
            const multiplicador = toInt(row.querySelector('.var-multi').value) || 1;

            total += estoque * multiplicador;
        });

        if (estoqueTotalInput) estoqueTotalInput.value = total;
        if (estoqueTotalBadge) estoqueTotalBadge.textContent = total;

        return total;
    }

    function validarPrecoPromocional(row) {
        const precoInput = row.querySelector('.var-preco');
        const promoInput = row.querySelector('.var-promo');

        if (!precoInput || !promoInput) return true;

        const preco = parseMoneyBR(precoInput.value);
        const promo = parseMoneyBR(promoInput.value);

        promoInput.classList.remove('is-invalid', 'is-valid');

        if (promo <= 0 || promoInput.value.trim() === '') {
            promoInput.classList.remove('is-invalid');
            return true;
        }

        if (promo < preco) {
            promoInput.classList.remove('is-invalid');
            return true;
        } else {
            promoInput.classList.add('is-invalid');
            return false;
        }
    }

    // ============== FUNÇÕES DE AÇÃO ==============

    function adicionarVariacao() {
        const rows = varsBody.querySelectorAll('.var-row');
        const novoIndex = rows.length;

        const novaLinha = document.createElement('tr');
        novaLinha.className = 'var-row';
        novaLinha.dataset.index = novoIndex;

        novaLinha.innerHTML = `
            <td>
                <input type="hidden" name="Variacoes[${novoIndex}].Id" value="" />
                <input class="form-control" 
                       name="Variacoes[${novoIndex}].Nome" 
                       placeholder="Ex: Caixa com 12 unidades" 
                       required />
            </td>
            <td>
                <input class="form-control var-multi" 
                       name="Variacoes[${novoIndex}].Multiplicador" 
                       value="1" 
                       type="number" 
                       min="1" 
                       step="1" 
                       required />
            </td>
            <td>
                <input class="form-control var-money var-preco" 
                       name="Variacoes[${novoIndex}].PrecoTexto" 
                       value="" 
                       type="text" 
                       placeholder="0,00"
                       required />
            </td>
            <td>
                <input class="form-control var-money var-promo" 
                       name="Variacoes[${novoIndex}].PrecoPromocionalTexto" 
                       value="" 
                       type="text" 
                       placeholder="0,00 (opcional)" />
            </td>
            <td>
                <input class="form-control var-estoque" 
                       name="Variacoes[${novoIndex}].Estoque" 
                       value="0" 
                       type="number" 
                       min="0" 
                       step="1" />
            </td>
            <td class="text-center">
                <input class="form-check-input var-ativo" 
                       type="checkbox" 
                       name="Variacoes[${novoIndex}].Ativo" 
                       value="true" 
                       checked />
                <input type="hidden" name="Variacoes[${novoIndex}].Ativo" value="false" />
            </td>
            <td class="text-end">
                <button type="button" class="btn btn-outline-danger btn-sm btnRemoveVar" title="Remover">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        `;

        varsBody.appendChild(novaLinha);
        renumberRows();

        const novosInputs = novaLinha.querySelectorAll('input');
        novosInputs.forEach(input => {
            if (input.classList.contains('var-money')) {
                input.addEventListener('input', function (e) {
                    formatMoneyInput(e.target);
                });

                input.addEventListener('blur', function () {
                    setTimeout(() => {
                        validarPrecoPromocional(novaLinha);
                    }, 100);
                });
            }
        });

        novaLinha.querySelector('[name$=".Nome"]').focus();
        calcularEstoqueTotal();
    }

    function removerVariacao(btn) {
        const row = btn.closest('.var-row');
        const rows = varsBody.querySelectorAll('.var-row');

        if (rows.length <= 1) {
            alert('É necessário ter pelo menos uma variação.');
            return;
        }

        if (confirm('Deseja realmente remover esta variação?')) {
            row.remove();
            renumberRows();
            calcularEstoqueTotal();
        }
    }

    // ============== INICIALIZAÇÃO ==============

    function formatarValoresExistentes() {
        varsBody.querySelectorAll('.var-money').forEach(input => {
            const valor = input.value;
            if (valor && valor.trim() !== '') {
                const numero = parseMoneyBR(valor);
                if (numero > 0) {
                    if (input.classList.contains('var-preco') && Math.abs(numero - 0.01) < 0.0001) {
                        input.value = '0,01';
                    } else {
                        input.value = formatMoneyBR(numero, true);
                    }
                } else {
                    if (input.classList.contains('var-promo')) {
                        input.setAttribute('placeholder', '0,00 (opcional)');
                    } else {
                        input.setAttribute('placeholder', '0,00');
                    }
                }
            } else {
                if (input.classList.contains('var-promo')) {
                    input.setAttribute('placeholder', '0,00 (opcional)');
                } else {
                    input.setAttribute('placeholder', '0,00');
                }
            }
        });
    }

    function adicionarEventos() {
        varsBody.querySelectorAll('.var-money').forEach(input => {
            if (input.classList.contains('var-promo')) {
                input.setAttribute('placeholder', '0,00 (opcional)');
            } else {
                input.setAttribute('placeholder', '0,00');
            }

            input.addEventListener('input', function (e) {
                formatMoneyInput(e.target);
            });

            input.addEventListener('blur', function () {
                const row = this.closest('.var-row');
                if (row) {
                    setTimeout(() => {
                        validarPrecoPromocional(row);
                    }, 100);
                }

                if (this.value === '' && this.classList.contains('var-promo')) {
                    this.setAttribute('placeholder', '0,00 (opcional)');
                }
            });

            input.addEventListener('focus', function () {
                const currentPlaceholder = this.getAttribute('placeholder');
                this.dataset.originalPlaceholder = currentPlaceholder;
                this.setAttribute('placeholder', '');
            });

            input.addEventListener('blur', function () {
                if (this.value === '') {
                    const originalPlaceholder = this.dataset.originalPlaceholder ||
                        (this.classList.contains('var-promo') ? '0,00 (opcional)' : '0,00');
                    this.setAttribute('placeholder', originalPlaceholder);
                }
            });
        });

        varsBody.querySelectorAll('.var-estoque, .var-multi, .var-ativo').forEach(input => {
            input.addEventListener('change', calcularEstoqueTotal);
            input.addEventListener('input', calcularEstoqueTotal);
        });

        btnAddVar.addEventListener('click', adicionarVariacao);

        varsBody.addEventListener('click', function (e) {
            if (e.target.closest('.btnRemoveVar')) {
                removerVariacao(e.target.closest('.btnRemoveVar'));
            }
        });

        varsBody.querySelectorAll('.var-row').forEach(row => {
            validarPrecoPromocional(row);
        });
    }

    function inicializar() {
        varsBody.querySelectorAll('.var-preco').forEach(input => {
            if (input.value === '0,01' || parseMoneyBR(input.value) === 0.01) {
                input.value = '0,01';
            }
        });

        formatarValoresExistentes();
        adicionarEventos();
        calcularEstoqueTotal();
    }

    inicializar();

    // ============== VALIDAÇÃO DO FORMULÁRIO ==============

    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function (event) {
            let isValid = true;
            const errorMessages = [];

            varsBody.querySelectorAll('.var-row').forEach((row, index) => {
                const nome = row.querySelector('[name$=".Nome"]').value.trim();
                const preco = row.querySelector('.var-preco').value.trim();
                const multiplicador = row.querySelector('.var-multi').value;

                if (!nome) {
                    isValid = false;
                    errorMessages.push(`Variação ${index + 1}: Nome é obrigatório`);
                }

                if (!preco || parseMoneyBR(preco) <= 0) {
                    isValid = false;
                    errorMessages.push(`Variação ${index + 1}: Preço inválido (deve ser maior que 0)`);
                }

                if (!multiplicador || parseInt(multiplicador) < 1) {
                    isValid = false;
                    errorMessages.push(`Variação ${index + 1}: Multiplicador deve ser maior que 0`);
                }

                if (!validarPrecoPromocional(row)) {
                    isValid = false;
                    errorMessages.push(`Variação ${index + 1}: Preço promocional deve ser menor que o preço normal`);
                }
            });

            if (!isValid) {
                event.preventDefault();
                event.stopPropagation();

                let errorHtml = '<div class="alert alert-danger">';
                errorHtml += '<h6>Corrija os seguintes erros:</h6><ul class="mb-0">';
                errorMessages.forEach(msg => {
                    errorHtml += `<li>${msg}</li>`;
                });
                errorHtml += '</ul></div>';

                const validationSummary = document.querySelector('[asp-validation-summary]');
                if (validationSummary) {
                    validationSummary.innerHTML = errorHtml;
                } else {
                    const errorDiv = document.createElement('div');
                    errorDiv.innerHTML = errorHtml;
                    form.prepend(errorDiv);
                }

                window.scrollTo({ top: 0, behavior: 'smooth' });
            }
        });
    }
});