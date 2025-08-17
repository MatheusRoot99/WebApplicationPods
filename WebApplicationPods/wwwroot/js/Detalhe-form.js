document.addEventListener("DOMContentLoaded", function () {
    /* -------------------------
       Tabs
    ------------------------- */
    const tabButtons = document.querySelectorAll(".tab-button");
    const tabPanes = document.querySelectorAll(".tab-pane");

    tabButtons.forEach(button => {
        button.addEventListener("click", function () {
            const tabId = this.getAttribute("data-tab");
            tabButtons.forEach(btn => btn.classList.remove("active"));
            tabPanes.forEach(pane => pane.classList.remove("active"));
            this.classList.add("active");
            document.getElementById(tabId).classList.add("active");
        });
    });

    /* -------------------------
       FAQ Accordion
    ------------------------- */
    const faqQuestions = document.querySelectorAll(".faq-question");
    faqQuestions.forEach(question => {
        question.addEventListener("click", function () {
            const item = this.parentNode;
            const answer = this.nextElementSibling;
            const icon = this.querySelector("i");
            item.classList.toggle("active");

            if (item.classList.contains("active")) {
                icon.classList.replace("fa-chevron-down", "fa-chevron-up");
                answer.style.maxHeight = answer.scrollHeight + "px";
            } else {
                icon.classList.replace("fa-chevron-up", "fa-chevron-down");
                answer.style.maxHeight = null;
            }
        });
    });

    // Abre o primeiro item do FAQ automaticamente
    const firstFaqItem = document.querySelector(".faq-item");
    if (firstFaqItem) {
        firstFaqItem.classList.add("active");
        const firstAnswer = firstFaqItem.querySelector(".faq-answer");
        const firstIcon = firstFaqItem.querySelector(".faq-question i");
        firstAnswer.style.maxHeight = firstAnswer.scrollHeight + "px";
        firstIcon.classList.replace("fa-chevron-down", "fa-chevron-up");
    }

    /* -------------------------
       Controle de Quantidade
    ------------------------- */
    /* -------------------------
       Controle de Quantidade
    ------------------------- */
    const minusBtn = document.querySelector(".quantity-btn.minus");
    const plusBtn = document.querySelector(".quantity-btn.plus");
    const quantityInput = document.querySelector(".quantity-input");
    const quantidadeHidden = document.getElementById("quantidadeHidden");

    // Estoque inicial (caso não haja sabores)
    let estoqueMaximo = parseInt(document.getElementById("estoque-quantidade")?.textContent || 10);

    function atualizarQuantidade(valor) {
        let value = parseInt(valor) || 1;
        value = Math.min(Math.max(value, 1), estoqueMaximo); // clamp entre 1 e estoqueMaximo
        quantityInput.value = value;
        quantidadeHidden.value = value;
    }

    if (minusBtn && plusBtn && quantityInput && quantidadeHidden) {
        minusBtn.addEventListener("click", function () {
            atualizarQuantidade(parseInt(quantityInput.value) - 1);
        });

        plusBtn.addEventListener("click", function () {
            atualizarQuantidade(parseInt(quantityInput.value) + 1);
        });

        quantityInput.addEventListener("change", function () {
            atualizarQuantidade(this.value);
        });
    }

    /* -------------------------
       Controle de Sabores e Estoque
    ------------------------- */
    /* -------------------------
       Controle de Sabores e Estoque
    ------------------------- */
    const flavorRadios = document.querySelectorAll(".flavor-radio");
    const saborSelecionado = document.getElementById("saborSelecionado");
    const estoqueInfo = document.getElementById("estoque-info");
    const estoqueQuantidade = document.getElementById("estoque-quantidade");
    const addToCartBtn = document.querySelector(".btn-add-to-cart");

    function atualizarEstoque() {
        const selecionado = document.querySelector(".flavor-radio:checked");

        if (selecionado) {
            const estoque = parseInt(selecionado.getAttribute("data-estoque")) || 0;
            estoqueMaximo = estoque; // atualiza o limite global
            if (saborSelecionado) saborSelecionado.value = selecionado.value;

            if (estoqueInfo && estoqueQuantidade) {
                estoqueQuantidade.textContent = estoque;
                estoqueInfo.style.display = "block";
            }

            // Ajusta a quantidade atual para não ultrapassar o novo estoque
            atualizarQuantidade(quantityInput.value);

            // Atualiza botão de adicionar ao carrinho
            if (addToCartBtn) {
                if (estoque <= 0) {
                    addToCartBtn.classList.add("disabled");
                    addToCartBtn.setAttribute("disabled", "disabled");
                    addToCartBtn.innerHTML =
                        '<i class="fas fa-shopping-cart"></i> ESGOTADO';
                } else {
                    addToCartBtn.classList.remove("disabled");
                    addToCartBtn.removeAttribute("disabled");
                    addToCartBtn.innerHTML =
                        '<i class="fas fa-shopping-cart"></i> ADICIONAR AO CARRINHO';
                }
            }
        } else {
            estoqueMaximo = parseInt(document.getElementById("estoque-quantidade")?.textContent || 10);
            if (estoqueInfo) estoqueInfo.style.display = "none";
            if (estoqueQuantidade) estoqueQuantidade.textContent = "";
            if (saborSelecionado) saborSelecionado.value = "";
        }
    }

    if (flavorRadios.length > 0) {
        flavorRadios.forEach(radio => {
            radio.addEventListener("change", atualizarEstoque);
        });
        atualizarEstoque(); // inicializa
    }

    /* -------------------------
      Validação antes de adicionar ao carrinho
   ------------------------- */
    const addToCartForm = document.getElementById("addToCartForm");
    if (addToCartForm) {
        addToCartForm.addEventListener("submit", function (e) {
            if (flavorRadios.length > 0 && !saborSelecionado.value) {
                e.preventDefault();
                alert("Por favor, selecione um sabor antes de adicionar ao carrinho.");
                return false;
            }
            return true;
        });
    }

    /* -------------------------
       Botão de Compra Rápida
    ------------------------- */
    /* -------------------------
       Botão de Compra Rápida
    ------------------------- */
    const buyNowBtn = document.querySelector(".btn-buy-now");
    if (buyNowBtn && !buyNowBtn.classList.contains("disabled")) {
        buyNowBtn.addEventListener("click", function () {
            if (flavorRadios.length > 0 && !saborSelecionado.value) {
                alert("Por favor, selecione um sabor antes de comprar.");
                return false;
            }

            // Adiciona parâmetro hidden para identificar compra rápida
            const input = document.createElement("input");
            input.type = "hidden";
            input.name = "buyNow";
            input.value = "true";
            addToCartForm.appendChild(input);

            addToCartForm.submit();
        });
    }

});
