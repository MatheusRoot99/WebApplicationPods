$(document).ready(function () {
    // Ativa/desativa preço promocional
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

    // Mostrar nome do arquivo selecionado
    $('#imagemUpload').change(function () {
        const fileName = $(this).val().split('\\').pop();
        $('.file-upload-text').text(fileName || 'Escolha uma imagem...');
        $('.file-upload-label').toggleClass('border-primary', !!fileName);
    });

    // Controle de sabores selecionados
    $('#addSaborBtn').click(function () {
        const sabor = $('#saborSelect').val();
        const saborText = $('#saborSelect option:selected').text();
        const quantidade = $('#quantidadeInput').val();

        if (!sabor) {
            showToast('Selecione um sabor primeiro', 'warning');
            return;
        }

        if (quantidade < 1 || isNaN(quantidade)) {
            showToast('Quantidade deve ser maior que zero', 'warning');
            return;
        }

        if ($(`input[value*='"Sabor":"${sabor}"']`).length > 0) {
            showToast('Este sabor já foi adicionado', 'info');
            return;
        }

        const saborData = {
            Sabor: sabor,
            Quantidade: quantidade
        };

        const newItem = $(`
            <div class="card mb-2 border-0 shadow-sm sabor-item">
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
                    <input type="hidden" name="SaboresQuantidadesList" value='${JSON.stringify(saborData)}'>
                </div>
            </div>
        `).hide().fadeIn(300);

        $('#saboresSelecionadosList .alert').remove();
        $('#saboresSelecionadosList').append(newItem);
        updateSaborCounter();
        $('#saborSelect').val('');
        $('#quantidadeInput').val(1);
    });

    // Remove sabor
    $('#saboresSelecionadosList').on('click', '.remove-sabor', function () {
        $(this).closest('.sabor-item').fadeOut(300, function () {
            $(this).remove();
            updateSaborCounter();
            if ($('#saboresSelecionadosList').children().length === 0) {
                $('#saboresSelecionadosList').html(`
                    <div class="alert alert-light border text-center py-4">
                        <i class="fas fa-ice-cream fa-2x text-muted mb-3"></i>
                        <p class="mb-0 text-muted">Nenhum sabor adicionado ainda</p>
                    </div>
                `);
            }
        });
    });

    // Atualiza contador
    function updateSaborCounter() {
        const count = $('#saboresSelecionadosList .sabor-item').length;
        $('#totalSabores').text(count);
    }

    // Mostra notificação
    function showToast(message, type) {
        const toast = $(`
            <div class="toast align-items-center text-white bg-${type} border-0 position-fixed bottom-0 end-0 m-3" role="alert" aria-live="assertive" aria-atomic="true">
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
});