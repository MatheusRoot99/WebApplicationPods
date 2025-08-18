document.addEventListener('DOMContentLoaded', function () {
    // Máscara para telefone
    $('#telefone').inputmask('(99) 99999-9999');

    // Validação do formulário
    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function (e) {
            const telefone = document.getElementById('telefone').value;
            const digitsOnly = telefone.replace(/\D/g, '');

            if (digitsOnly.length < 11) {
                e.preventDefault();
                alert('Por favor, informe um telefone válido com DDD');
            }
        });
    }

    // Efeito hover nos botões
    const buttons = document.querySelectorAll('.btn');
    buttons.forEach(button => {
        button.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-2px)';
        });
        button.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0)';
        });
    });
});