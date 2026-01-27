$(document).ready(function () {
    // 1. Controle do preço promocional
    $('#emPromocaoSwitch').change(function () {
        const isChecked = $(this).is(':checked');
        $('#precoPromocional').prop('disabled', !isChecked);
        if (isChecked) {
            $('#precoPromocional').closest('.mb-4').find('.input-group-text').addClass('text-primary');
        } else {
            $('#precoPromocional').closest('.mb-4').find('.input-group-text').removeClass('text-primary');
            $('#precoPromocional').val('');
        }
    }).trigger('change');

    // 2. Mostrar nome do arquivo selecionado
    $('#imagemUpload').change(function () {
        const fileName = $(this).val().split('\\').pop();
        $('.file-upload-text').text(fileName || 'Escolha uma imagem...');
        $('.file-upload-label').toggleClass('border-primary', !!fileName);
    });

    // 3. Adicionar sabor (selecionado ou novo)
    $('#addSaborBtn').click(function () {
        addSabor();
    });

    // 4. Permitir adicionar com Enter no campo de novo sabor
    $('#novoSaborInput').keypress(function (e) {
        if (e.which === 13) {
            addSabor();
            e.preventDefault();
        }
    });

    // 5. Função para adicionar sabor
    function addSabor() {
        let sabor = $('#saborSelect').val();
        let saborText = $('#saborSelect option:selected').text();
        const novoSabor = $('#novoSaborInput').val().trim();
        const quantidade = parseInt($('#quantidadeInput').val());

        // Verifica se está usando sabor selecionado ou novo sabor
        if (novoSabor) {
            sabor = novoSabor;
            saborText = novoSabor;
        }

        if (!sabor) {
            showToast('Selecione um sabor ou digite um novo', 'warning');
            return;
        }

        if (quantidade < 1 || isNaN(quantidade)) {
            showToast('Quantidade deve ser maior que zero', 'warning');
            return;
        }

        const existingItem = $(`.sabor-item[data-sabor="${sabor}"]`);
        if (existingItem.length > 0) {
            // Atualiza quantidade existente
            const currentQty = parseInt(existingItem.find('.quantidade-display').text());
            const newQty = currentQty + quantidade;

            updateQuantidadeSabor(existingItem, newQty);
            showToast('Quantidade atualizada para ' + saborText, 'info');
        } else {
            // Cria novo item
            const newItem = $(`
                <div class="card mb-2 border-0 shadow-sm sabor-item" data-sabor="${sabor}">
                    <div class="card-body py-2 px-3">
                        <div class="d-flex justify-content-between align-items-center">
                            <div class="flex-grow-1">
                                <span class="fw-bold text-primary sabor-nome">${saborText}</span>
                                <div class="mt-1">
                                    <span class="badge bg-light text-dark me-2">Quantidade:</span>
                                    <div class="btn-group btn-group-sm" role="group">
                                        <button type="button" class="btn btn-outline-secondary diminuir-qtd">
                                            <i class="fas fa-minus"></i>
                                        </button>
                                        <span class="btn btn-light quantidade-display" style="min-width: 50px;">${quantidade}</span>
                                        <button type="button" class="btn btn-outline-secondary aumentar-qtd">
                                            <i class="fas fa-plus"></i>
                                        </button>
                                    </div>
                                    <span class="badge bg-primary ms-2 quantidade-badge">${quantidade} un.</span>
                                </div>
                            </div>
                            <div class="ms-3">
                                <button type="button" class="btn btn-sm btn-link text-danger remove-sabor">
                                    <i class="fas fa-trash-alt"></i>
                                </button>
                            </div>
                        </div>
                        <input type="hidden" name="SaboresQuantidadesList" value='${JSON.stringify({
                sabor: sabor,
                quantidade: quantidade
            })}'>
                    </div>
                </div>
            `).hide().fadeIn(300);

            $('#saboresSelecionadosList .alert').remove();
            $('#saboresSelecionadosList').append(newItem);
        }

        updateCounters();

        // Limpa campos
        $('#saborSelect').val('');
        $('#novoSaborInput').val('');
        $('#quantidadeInput').val(1);
    }

    // 6. Aumentar quantidade de sabor existente
    $('#saboresSelecionadosList').on('click', '.aumentar-qtd', function () {
        const item = $(this).closest('.sabor-item');
        const currentQty = parseInt(item.find('.quantidade-display').text());
        const newQty = currentQty + 1;
        updateQuantidadeSabor(item, newQty);
        updateCounters();
    });

    // 7. Diminuir quantidade de sabor existente
    $('#saboresSelecionadosList').on('click', '.diminuir-qtd', function () {
        const item = $(this).closest('.sabor-item');
        const currentQty = parseInt(item.find('.quantidade-display').text());

        if (currentQty > 1) {
            const newQty = currentQty - 1;
            updateQuantidadeSabor(item, newQty);
            updateCounters();
        } else {
            showToast('Quantidade mínima é 1. Use o botão de lixeira para remover o sabor.', 'warning');
        }
    });

    // 8. Função para atualizar quantidade de um sabor
    function updateQuantidadeSabor(item, novaQuantidade) {
        const sabor = item.data('sabor');
        const saborText = item.find('.sabor-nome').text();

        item.find('.quantidade-display').text(novaQuantidade);
        item.find('.quantidade-badge').text(novaQuantidade + ' un.');

        // Atualiza o valor do input hidden
        item.find('input[name="SaboresQuantidadesList"]').val(JSON.stringify({
            sabor: sabor,
            quantidade: novaQuantidade
        }));

        showToast(`Quantidade de ${saborText} atualizada para ${novaQuantidade}`, 'info');
    }

    // 9. Remover sabor com confirmação
    $('#saboresSelecionadosList').on('click', '.remove-sabor', function () {
        const item = $(this).closest('.sabor-item');
        const saborText = item.find('.sabor-nome').text();

        if (confirm(`Remover o sabor "${saborText}"?`)) {
            item.fadeOut(300, function () {
                $(this).remove();
                updateCounters();

                if ($('#saboresSelecionadosList').children().length === 0) {
                    $('#saboresSelecionadosList').html(`
                        <div class="alert alert-light border text-center py-4">
                            <i class="fas fa-ice-cream fa-2x text-muted mb-3"></i>
                            <p class="mb-0 text-muted">Nenhum sabor adicionado ainda</p>
                        </div>
                    `);
                }
            });
        }
    });

    // 10. Atualizar contadores de sabores e estoque total
    function updateCounters() {
        try {
            const count = $('#saboresSelecionadosList .sabor-item').length;
            $('#totalSabores').text(isNaN(count) ? 0 : count);

            let totalEstoque = 0;

            $('input[name="SaboresQuantidadesList"]').each(function () {
                try {
                    const data = JSON.parse($(this).val());
                    const qty = parseInt(data.quantidade);

                    if (!isNaN(qty)) {
                        totalEstoque += qty;
                    }
                } catch (e) {
                    console.error('Erro ao processar sabor:', e);
                }
            });

            $('#totalEstoque').text('(' + (isNaN(totalEstoque) ? 0 : totalEstoque) + ' un.)');
            $('#Estoque').val(isNaN(totalEstoque) ? 0 : totalEstoque);
        } catch (error) {
            console.error('Erro ao atualizar contadores:', error);
            $('#totalSabores').text(0);
            $('#totalEstoque').text('(0 un.)');
            $('#Estoque').val(0);
        }
    }

    // 11. Toast genérico
    function showToast(message, type) {
        const toast = $(`
            <div class="toast align-items-center text-white bg-${type} border-0 position-fixed bottom-0 end-0 m-3" role="alert">
                <div class="d-flex">
                    <div class="toast-body">${message}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `);
        $('body').append(toast);
        new bootstrap.Toast(toast).show();
        setTimeout(() => toast.remove(), 3000);
    }

    // 12. Foco automático no campo de novo sabor quando selecionar "Selecione um sabor..."
    $('#saborSelect').change(function () {
        if ($(this).val() === '') {
            $('#novoSaborInput').focus();
        }
    });

    // 13. Inicialização
    updateCounters();
});