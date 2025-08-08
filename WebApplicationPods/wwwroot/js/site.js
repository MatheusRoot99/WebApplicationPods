// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
/ Função de busca simples
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


