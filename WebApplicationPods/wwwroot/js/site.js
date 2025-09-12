// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
//Função de busca simples
document.getElementById('searchInput')?.addEventListener('keyup', function () {
    const searchTerm = this.value.toLowerCase();
    document.querySelectorAll('.produto-card').forEach(card => {
        const title = card.querySelector('.card-title')?.textContent.toLowerCase();
        card.style.display = title?.includes(searchTerm) ? 'block' : 'none';
    });
});

// Inicialização de tooltips
document.addEventListener('DOMContentLoaded', function () {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
});


(function () {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    // Intercepta forms com a classe .js-add-to-cart e envia via fetch
    document.addEventListener('submit', async (ev) => {
        const form = ev.target;
        if (!form.classList.contains('js-add-to-cart')) return;

        ev.preventDefault();
        try {
            const fd = new FormData(form);
            if (token) fd.append('__RequestVerificationToken', token);

            const r = await fetch(form.action, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: fd
            });

            const data = await r.json();
            if (data?.ok) {
                // atualiza badges
                if (typeof window.updateCartBadges === 'function') {
                    window.updateCartBadges(data.count || 0);
                }

                // toast simples (se quiser)
                if (window.bootstrap) {
                    const toastBody = document.getElementById('appToastBody');
                    const toastEl = document.getElementById('appToast');
                    if (toastBody && toastEl) {
                        toastBody.textContent = `${data.nome || 'Produto'} adicionado ao carrinho!`;
                        toastEl.classList.remove('bg-danger'); toastEl.classList.add('bg-success');
                        new bootstrap.Toast(toastEl, { delay: 1800 }).show();
                    }
                }
            } else {
                // erro amigável
                alert(data?.msg || 'Não foi possível adicionar o item.');
            }
        } catch (e) {
            console.error(e);
            alert('Falha de conexão. Tente novamente.');
        }
    });
})();



