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

    // 3. Adicionar sabor
    $('#addSaborBtn').click(function () {
        const sabor = $('#saborSelect').val();
        const saborText = $('#saborSelect option:selected').text();
        const quantidade = parseInt($('#quantidadeInput').val());

        if (!sabor) {
            showToast('Selecione um sabor primeiro', 'warning');
            return;
        }

        if (quantidade < 1 || isNaN(quantidade)) {
            showToast('Quantidade deve ser maior que zero', 'warning');
            return;
        }

        const existingItem = $(`.sabor-item[data-sabor="${sabor}"]`);
        if (existingItem.length > 0) {
            // Atualiza quantidade existente
            const currentQty = parseInt(existingItem.find('.badge').text().replace(' un.', ''));
            const newQty = currentQty + quantidade;

            existingItem.find('.badge').text(newQty + ' un.');
            existingItem.find('input').val(JSON.stringify({
                sabor: sabor,
                quantidade: newQty // ← atualiza para nova quantidade total
            }));

            showToast('Quantidade atualizada para ' + saborText, 'info');
        } else {
            // Cria novo item
            const newItem = $(`
                <div class="card mb-2 border-0 shadow-sm sabor-item" data-sabor="${sabor}">
                    <div class="card-body py-2 px-3">
                        <div class="d-flex justify-content-between align-items-center">
                            <div>
                                <span class="fw-bold text-primary">${saborText}</span>
                                <span class="badge bg-light text-primary ms-2">${quantidade} un.</span>
                            </div>
                            <button type="button" class="btn btn-sm btn-link text-danger remove-sabor">
                                <i class="fas fa-trash-alt"></i>
                            </button>
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
        $('#saborSelect').val('');
        $('#quantidadeInput').val(1);
    });

    // 4. Remover sabor com confirmação
    $('#saboresSelecionadosList').on('click', '.remove-sabor', function () {
        const item = $(this).closest('.sabor-item');
        const saborText = item.find('.fw-bold').text();

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

    // 5. Atualizar contadores de sabores e estoque total
    function updateCounters() {
        try {
            const count = $('#saboresSelecionadosList .sabor-item').length;
            $('#totalSabores').text(isNaN(count) ? 0 : count);

            let totalEstoque = 0;

            $('input[name="SaboresQuantidadesList"]').each(function () {
                try {
                    const data = JSON.parse($(this).val());

                    // Corrigido para minúsculo (quantidade) — verifique se front e back estão alinhados
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

    // 6. Toast genérico
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

    // 7. Inicialização
    updateCounters();
});
