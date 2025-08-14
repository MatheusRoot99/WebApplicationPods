document.addEventListener('DOMContentLoaded', function () {
    // Tabs
    const tabButtons = document.querySelectorAll('.tab-button');
    const tabPanes = document.querySelectorAll('.tab-pane');

    tabButtons.forEach(button => {
        button.addEventListener('click', function () {
            const tabId = this.getAttribute('data-tab');
            tabButtons.forEach(btn => btn.classList.remove('active'));
            tabPanes.forEach(pane => pane.classList.remove('active'));
            this.classList.add('active');
            document.getElementById(tabId).classList.add('active');
        });
    });

    // FAQ Accordion
    const faqQuestions = document.querySelectorAll('.faq-question');
    faqQuestions.forEach(question => {
        question.addEventListener('click', function () {
            const item = this.parentNode;
            const answer = this.nextElementSibling;
            const icon = this.querySelector('i');
            item.classList.toggle('active');

            if (item.classList.contains('active')) {
                icon.classList.replace('fa-chevron-down', 'fa-chevron-up');
                answer.style.maxHeight = answer.scrollHeight + 'px';
            } else {
                icon.classList.replace('fa-chevron-up', 'fa-chevron-down');
                answer.style.maxHeight = null;
            }
        });
    });

    // Inicializa primeiro item do FAQ como aberto
    const firstFaqItem = document.querySelector('.faq-item');
    if (firstFaqItem) {
        firstFaqItem.classList.add('active');
        const firstAnswer = firstFaqItem.querySelector('.faq-answer');
        const firstIcon = firstFaqItem.querySelector('.faq-question i');
        firstAnswer.style.maxHeight = firstAnswer.scrollHeight + 'px';
        firstIcon.classList.replace('fa-chevron-down', 'fa-chevron-up');
    }

    // Controle de quantidade
    const minusBtn = document.querySelector('.quantity-btn.minus');
    const plusBtn = document.querySelector('.quantity-btn.plus');
    const quantityInput = document.querySelector('.quantity-input');
    const quantidadeHidden = document.getElementById('quantidadeHidden');

    if (minusBtn && plusBtn && quantityInput && quantidadeHidden) {
        minusBtn.addEventListener('click', function () {
            let value = parseInt(quantityInput.value) || 1;
            quantityInput.value = value > 1 ? value - 1 : 1;
            quantidadeHidden.value = quantityInput.value;
        });

        plusBtn.addEventListener('click', function () {
            let value = parseInt(quantityInput.value) || 1;
            quantityInput.value = value < 10 ? value + 1 : 10;
            quantidadeHidden.value = quantityInput.value;
        });

        quantityInput.addEventListener('change', function () {
            let value = parseInt(this.value) || 1;
            this.value = Math.min(Math.max(value, 1), 10);
            quantidadeHidden.value = this.value;
        });
    }

    // Captura do sabor selecionado e exibe o estoque
    const flavorRadios = document.querySelectorAll('.flavor-radio');
    const saborSelecionado = document.getElementById('saborSelecionado');
    const estoqueInfo = document.getElementById('estoque-info');
    const estoqueQuantidade = document.getElementById('estoque-quantidade');

    if (flavorRadios.length && saborSelecionado) {
        flavorRadios.forEach(radio => {
            radio.addEventListener('change', function () {
                if (this.checked) {
                    saborSelecionado.value = this.value;

                    const estoque = this.getAttribute('data-estoque');
                    if (estoqueInfo && estoqueQuantidade) {
                        estoqueQuantidade.textContent = estoque;
                        estoqueInfo.style.display = 'block';
                    }
                } else {
                    estoqueInfo.style.display = 'none';
                    estoqueQuantidade.textContent = '';
                }
            });
        });
    }


    // Validação antes de adicionar ao carrinho
    const addToCartForm = document.querySelector('form[asp-controller="Carrinho"]');
    if (addToCartForm) {
        addToCartForm.addEventListener('submit', function (e) {
            if (flavorRadios.length && !saborSelecionado.value) {
                e.preventDefault();
                alert('Por favor, selecione um sabor antes de adicionar ao carrinho.');
                return false;
            }
            return true;
        });
    }

    // Botão de compra rápida
    const buyNowBtn = document.querySelector('.btn-buy-now');
    if (buyNowBtn && !buyNowBtn.classList.contains('disabled')) {
        buyNowBtn.addEventListener('click', function () {
            if (flavorRadios.length && !saborSelecionado.value) {
                alert('Por favor, selecione um sabor antes de comprar.');
                return false;
            }
            addToCartForm.submit();
        });
    }
});
document.addEventListener("DOMContentLoaded", function () {
    const radios = document.querySelectorAll(".flavor-radio");
    const estoqueInfo = document.getElementById("estoque-info");
    const estoqueQuantidade = document.getElementById("estoque-quantidade");
    const inputSaborSelecionado = document.getElementById("saborSelecionado");

    function atualizarEstoque() {
        const selecionado = document.querySelector(".flavor-radio:checked");
        if (selecionado) {
            const estoque = selecionado.getAttribute("data-estoque");
            estoqueQuantidade.textContent = estoque;
            estoqueInfo.style.display = "block";

            // Atualiza o input hidden do sabor selecionado para envio no form
            if (inputSaborSelecionado) {
                inputSaborSelecionado.value = selecionado.value;
            }
        } else {
            estoqueInfo.style.display = "none";
            estoqueQuantidade.textContent = "";
            if (inputSaborSelecionado) {
                inputSaborSelecionado.value = "";
            }
        }
    }

    radios.forEach(radio => {
        radio.addEventListener("change", atualizarEstoque);
    });

    // Opcional: atualiza o estoque ao carregar a página se algum sabor já estiver selecionado
    atualizarEstoque();
});

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////carrinho

// Controle de quantidade
const minusBtn = document.querySelector('.quantity-btn.minus');
const plusBtn = document.querySelector('.quantity-btn.plus');
const quantityInput = document.querySelector('.quantity-input');
const quantidadeHidden = document.getElementById('quantidadeHidden');

if (minusBtn && plusBtn && quantityInput && quantidadeHidden) {
    minusBtn.addEventListener('click', function () {
        let value = parseInt(quantityInput.value) || 1;
        quantityInput.value = value > 1 ? value - 1 : 1;
        quantidadeHidden.value = quantityInput.value;
    });

    plusBtn.addEventListener('click', function () {
        let value = parseInt(quantityInput.value) || 1;
        quantityInput.value = value < 10 ? value + 1 : 10;
        quantidadeHidden.value = quantityInput.value;
    });

    quantityInput.addEventListener('change', function () {
        let value = parseInt(this.value) || 1;
        this.value = Math.min(Math.max(value, 1), 10);
        quantidadeHidden.value = this.value;
    });
}

// Captura do sabor selecionado e exibe o estoque
const flavorRadios = document.querySelectorAll('.flavor-radio');
const saborSelecionado = document.getElementById('saborSelecionado');
const estoqueInfo = document.getElementById('estoque-info');
const estoqueQuantidade = document.getElementById('estoque-quantidade');

if (flavorRadios.length && saborSelecionado) {
    flavorRadios.forEach(radio => {
        radio.addEventListener('change', function () {
            if (this.checked) {
                saborSelecionado.value = this.value;

                const estoque = this.getAttribute('data-estoque');
                if (estoqueInfo && estoqueQuantidade) {
                    estoqueQuantidade.textContent = estoque;
                    estoqueInfo.style.display = 'block';

                    // Atualiza o estado do botão de adicionar ao carrinho
                    const addToCartBtn = document.querySelector('.btn-add-to-cart');
                    if (parseInt(estoque) <= 0) {
                        addToCartBtn.classList.add('disabled');
                        addToCartBtn.setAttribute('disabled', 'disabled');
                        addToCartBtn.innerHTML = '<i class="fas fa-shopping-cart"></i> ESGOTADO';
                    } else {
                        addToCartBtn.classList.remove('disabled');
                        addToCartBtn.removeAttribute('disabled');
                        addToCartBtn.innerHTML = '<i class="fas fa-shopping-cart"></i> ADICIONAR AO CARRINHO';
                    }
                }
            }
        });
    });
}

// Validação antes de adicionar ao carrinho
const addToCartForm = document.getElementById('addToCartForm');
if (addToCartForm) {
    addToCartForm.addEventListener('submit', function (e) {
        if (flavorRadios.length > 0 && !saborSelecionado.value) {
            e.preventDefault();
            alert('Por favor, selecione um sabor antes de adicionar ao carrinho.');
            return false;
        }
        return true;
    });
}

// Botão de compra rápida
const buyNowBtn = document.querySelector('.btn-buy-now');
if (buyNowBtn && !buyNowBtn.classList.contains('disabled')) {
    buyNowBtn.addEventListener('click', function () {
        if (flavorRadios.length > 0 && !saborSelecionado.value) {
            alert('Por favor, selecione um sabor antes de comprar.');
            return false;
        }

        // Adiciona um parâmetro para identificar compra rápida
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = 'buyNow';
        input.value = 'true';
        addToCartForm.appendChild(input);

        addToCartForm.submit();
    });
}