// =========================================================
// Produto Simples - JS
// Cadastro simples de produto / bebida / pod
// =========================================================

// jQuery Validate: aceitar 1.234,56 como number pt-BR
(function () {
    function initValidation() {
        if (!window.jQuery || !jQuery.validator) {
            setTimeout(initValidation, 100);
            return;
        }

        jQuery.validator.methods.number = function (value, element) {
            if (this.optional(element)) return true;

            value = (value || "").toString().trim().replace(/\s/g, "");

            if (value.indexOf(",") > -1) {
                value = value.replace(/\./g, "").replace(",", ".");
            }

            return /^-?\d+(\.\d+)?$/.test(value);
        };

        if (jQuery.validator.methods.range) {
            const originalRange = jQuery.validator.methods.range;

            jQuery.validator.methods.range = function (value, element, param) {
                value = (value || "").toString().trim().replace(/\s/g, "");

                if (value.indexOf(",") > -1) {
                    if (value.indexOf(".") > -1 && value.indexOf(",") > value.indexOf(".")) {
                        value = value.replace(/\./g, "");
                    }

                    value = value.replace(",", ".");
                }

                return originalRange.call(this, value, element, param);
            };
        }
    }

    initValidation();
})();

(function () {
    function qs(sel) {
        return document.querySelector(sel);
    }

    function qsa(sel) {
        return Array.from(document.querySelectorAll(sel));
    }

    function onlyDigits(value) {
        return (value || "").replace(/\D+/g, "");
    }

    function formatMoneyBRFromDigits(digits) {
        digits = (digits || "").replace(/^0+/, "") || "0";

        if (digits.length === 1) digits = "0" + digits;
        if (digits.length === 2) digits = "0" + digits;

        const cents = digits.slice(-2);
        let intPart = digits.slice(0, -2);

        intPart = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");

        return intPart + "," + cents;
    }

    function normalizeMoneyToInvariant(value) {
        if (!value) return "";

        let s = String(value).trim();
        s = s.replace(/[^\d,.\-]/g, "");

        if (s.includes(",") && s.includes(".")) {
            s = s.replace(/\./g, "").replace(",", ".");
        } else {
            s = s.replace(",", ".");
        }

        return s;
    }

    function toNumberInvariant(value) {
        const normalized = normalizeMoneyToInvariant(value);
        const number = parseFloat(normalized);

        return isNaN(number) ? 0 : number;
    }

    function setInvalid(el, invalid) {
        if (!el) return;
        el.classList.toggle("ps-invalid", !!invalid);
    }

    function show(el, visible) {
        if (!el) return;
        el.style.display = visible ? "" : "none";
    }

    function getEnumConfig() {
        const cfg = document.getElementById("psEnums");

        return {
            PADRAO: cfg?.getAttribute("data-padrao") || "",
            POD: cfg?.getAttribute("data-pod") || "",
            BEBIDA: cfg?.getAttribute("data-bebida") || ""
        };
    }

    function getCurrentTipo() {
        return String(tipoEl?.value || "");
    }

    function isTipoPadrao() {
        const { PADRAO } = getEnumConfig();
        return getCurrentTipo() === String(PADRAO);
    }

    function isTipoPod() {
        const { POD } = getEnumConfig();
        return getCurrentTipo() === String(POD);
    }

    function isTipoBebida() {
        const { BEBIDA } = getEnumConfig();
        return getCurrentTipo() === String(BEBIDA);
    }

    function attachMoneyMask(input, onChange) {
        if (!input) return;

        function apply() {
            const digits = onlyDigits(input.value);

            input.value = formatMoneyBRFromDigits(digits);
            input.dataset.rawDigits = digits;

            if (onChange) onChange();
        }

        input.addEventListener("input", apply);
        input.addEventListener("blur", apply);
    }

    function validatePromo() {
        if (!precoEl || !promoEl) return true;

        const preco = toNumberInvariant(precoEl.value);
        const promo = toNumberInvariant(promoEl.value);

        if (!promoEl.value || promo <= 0) {
            setInvalid(promoEl, false);

            if (promoHintEl) {
                promoHintEl.textContent = "";
            }

            return true;
        }

        if (preco <= 0 || promo >= preco) {
            setInvalid(promoEl, true);

            if (promoHintEl) {
                promoHintEl.textContent = "Promo deve ser menor que o preço.";
            }

            return false;
        }

        setInvalid(promoEl, false);

        if (promoHintEl) {
            promoHintEl.textContent = "Promo válida.";
        }

        return true;
    }

    function initMoneyFields() {
        qsa(".js-money").forEach(function (el) {
            attachMoneyMask(el, validatePromo);

            if (el.value) {
                const normalized = normalizeMoneyToInvariant(el.value);
                const digits = onlyDigits(normalized.replace(".", ""));

                el.value = formatMoneyBRFromDigits(digits);
                el.dataset.rawDigits = digits;
            }
        });

        validatePromo();
    }

    function syncSaborHidden() {
        if (!saborHiddenEl) return;

        if (!isTipoPod()) {
            saborHiddenEl.value = "";
            return;
        }

        const outro = (saborOutroEl?.value || "").trim();
        const selected = (saborSelectEl?.value || "").trim();

        saborHiddenEl.value = outro.length > 0 ? outro : selected;
    }

    function initSaborPod() {
        if (saborSelectEl) {
            saborSelectEl.addEventListener("change", syncSaborHidden);
        }

        if (saborOutroEl) {
            saborOutroEl.addEventListener("input", syncSaborHidden);
        }

        const current = (saborHiddenEl?.value || "").trim();

        if (current && saborSelectEl) {
            const option = Array.from(saborSelectEl.options)
                .find(o => (o.value || "").trim().toLowerCase() === current.toLowerCase());

            if (option) {
                saborSelectEl.value = option.value;
            } else if (saborOutroEl) {
                saborOutroEl.value = current;
            }
        }

        syncSaborHidden();
    }

    function syncNomePlaceholder() {
        if (!nomeEl) return;

        if (isTipoPod()) {
            nomeEl.placeholder = "Ex: Ignite 28000 Puffs Banana Ice";
            return;
        }

        if (isTipoBebida()) {
            nomeEl.placeholder = "Ex: Heineken 330ml Pack";
            return;
        }

        nomeEl.placeholder = "Ex: Chocolate Lacta 90g";
    }

    function clearPodFieldsWhenNeeded() {
        if (isTipoPod()) return;

        if (saborSelectEl) saborSelectEl.value = "";
        if (saborOutroEl) saborOutroEl.value = "";
        if (saborHiddenEl) saborHiddenEl.value = "";

        const podPuffsEl = qs('input[name="PodPuffs"]');
        const podBateriaEl = qs('input[name="PodCapacidadeBateria"]');
        const podTipoEl = qs('input[name="PodTipo"]');

        if (podPuffsEl) podPuffsEl.value = "";
        if (podBateriaEl) podBateriaEl.value = "";
        if (podTipoEl) podTipoEl.value = "";
    }

    function clearBebidaFieldsWhenNeeded() {
        if (isTipoBebida()) return;

        const bebidaTipoEl = qs('input[name="BebidaTipo"]');
        const bebidaVolumeEl = qs('input[name="BebidaVolumeMl"]');

        if (bebidaTipoEl) bebidaTipoEl.value = "";
        if (bebidaVolumeEl) bebidaVolumeEl.value = "";

        if (bebidaEmbalagemEl) bebidaEmbalagemEl.value = bebidaEmbalagemEl.querySelector("option")?.value || "";
        if (bebidaQtdPorEmbalagemEl) bebidaQtdPorEmbalagemEl.value = "";
    }

    function syncTypeBlocks() {
        const pod = isTipoPod();
        const bebida = isTipoBebida();

        show(saborFieldWrap, pod);
        show(podDetailsBox, pod);
        show(bebidaDetailsBox, bebida);

        if (estoqueHintEl) {
            estoqueHintEl.textContent = bebida
                ? "Informe quantas embalagens você tem para vender. Ex.: 6 packs."
                : "Informe quantas unidades estão disponíveis.";
        }

        clearPodFieldsWhenNeeded();
        clearBebidaFieldsWhenNeeded();

        syncSaborHidden();
        syncNomePlaceholder();
        updateEstoqueResumo();
    }

    function previewImagem() {
        if (!imgEl || !imgPreviewEl || !imgPreviewEmptyEl) return;

        imgEl.addEventListener("change", function () {
            const file = imgEl.files && imgEl.files[0];

            if (!file) return;

            if (!file.type || !file.type.startsWith("image/")) {
                imgEl.value = "";
                return;
            }

            const url = URL.createObjectURL(file);

            imgPreviewEl.src = url;
            imgPreviewEl.classList.remove("d-none");
            imgPreviewEmptyEl.classList.add("d-none");
        });
    }

    function getSelectedEmbalagemText() {
        if (!bebidaEmbalagemEl) return "";

        const selectedOption = bebidaEmbalagemEl.options[bebidaEmbalagemEl.selectedIndex];

        return (selectedOption?.text || "").trim();
    }

    function isEmbalagemCompostaByText(text) {
        const normalized = (text || "").trim().toLowerCase();

        return normalized === "pack" ||
            normalized === "fardo" ||
            normalized === "caixa";
    }

    function pluralizarEmbalagem(text) {
        const normalized = (text || "").trim().toLowerCase();

        switch (normalized) {
            case "pack":
                return "packs";
            case "fardo":
                return "fardos";
            case "caixa":
                return "caixas";
            case "lata":
                return "latas";
            case "garrafa":
                return "garrafas";
            case "long neck":
                return "long necks";
            case "unidade":
                return "unidades";
            default:
                return normalized.endsWith("s") ? text : `${text}s`;
        }
    }

    function updateEstoqueResumo() {
        if (!bebidaEstoqueResumoEl) return;

        if (!isTipoBebida()) {
            bebidaEstoqueResumoEl.textContent = "Este produto será vendido por unidade.";
            return;
        }

        const estoque = Math.max(parseInt(estoqueEl?.value || "0", 10) || 0, 0);
        const qtdPorEmbalagem = Math.max(parseInt(bebidaQtdPorEmbalagemEl?.value || "0", 10) || 0, 0);
        const embalagemText = getSelectedEmbalagemText();
        const embalagemNormalizada = embalagemText.toLowerCase();

        if (!embalagemText || embalagemNormalizada === "não informado") {
            bebidaEstoqueResumoEl.textContent = "Selecione a embalagem para calcular o estoque físico.";
            return;
        }

        if (!isEmbalagemCompostaByText(embalagemText)) {
            const unidadeTexto = estoque === 1 ? "unidade" : "unidades";
            bebidaEstoqueResumoEl.textContent = `${estoque} ${unidadeTexto} para venda.`;
            return;
        }

        if (qtdPorEmbalagem <= 1) {
            bebidaEstoqueResumoEl.textContent = "Informe quantas unidades vêm em cada embalagem.";
            return;
        }

        const embalagemVenda = estoque === 1
            ? embalagemText.toLowerCase()
            : pluralizarEmbalagem(embalagemText);

        const totalFisico = estoque * qtdPorEmbalagem;
        const unidadeFisicaTexto = totalFisico === 1 ? "unidade física" : "unidades físicas";

        bebidaEstoqueResumoEl.textContent =
            `${estoque} ${embalagemVenda} com ${qtdPorEmbalagem} unidades cada = ${totalFisico} ${unidadeFisicaTexto}.`;
    }

    function initDescricaoCounter() {
        if (!descricaoEl || !descricaoCounterEl) return;

        function update() {
            const total = (descricaoEl.value || "").length;
            descricaoCounterEl.textContent = `${total}/1000 caracteres`;
        }

        descricaoEl.addEventListener("input", update);
        update();
    }

    function beforeSubmitNormalizeMoney() {
        if (precoEl) {
            precoEl.value = normalizeMoneyToInvariant(precoEl.value);
        }

        if (promoEl) {
            promoEl.value = normalizeMoneyToInvariant(promoEl.value);
        }

        syncSaborHidden();
    }

    function validatePodRequired() {
        if (!isTipoPod()) return true;

        syncSaborHidden();

        const sabor = (saborHiddenEl?.value || "").trim();

        if (!sabor) {
            setInvalid(saborSelectEl, true);
            setInvalid(saborOutroEl, true);

            if (saborSelectEl) saborSelectEl.focus();

            return false;
        }

        setInvalid(saborSelectEl, false);
        setInvalid(saborOutroEl, false);

        return true;
    }

    function validateBebidaResumo() {
        if (!isTipoBebida()) return true;

        const embalagemText = getSelectedEmbalagemText();

        if (!isEmbalagemCompostaByText(embalagemText)) {
            return true;
        }

        const qtdPorEmbalagem = Math.max(parseInt(bebidaQtdPorEmbalagemEl?.value || "0", 10) || 0, 0);

        if (qtdPorEmbalagem <= 1) {
            setInvalid(bebidaQtdPorEmbalagemEl, true);

            if (bebidaQtdPorEmbalagemEl) {
                bebidaQtdPorEmbalagemEl.focus();
            }

            return false;
        }

        setInvalid(bebidaQtdPorEmbalagemEl, false);

        return true;
    }

    let formEl;

    let precoEl;
    let promoEl;
    let promoHintEl;

    let tipoEl;
    let nomeEl;
    let descricaoEl;
    let descricaoCounterEl;

    let estoqueEl;
    let estoqueHintEl;

    let imgEl;
    let imgPreviewEl;
    let imgPreviewEmptyEl;

    let saborFieldWrap;
    let podDetailsBox;
    let bebidaDetailsBox;

    let saborSelectEl;
    let saborOutroEl;
    let saborHiddenEl;

    let bebidaEmbalagemEl;
    let bebidaQtdPorEmbalagemEl;
    let bebidaEstoqueResumoEl;

    document.addEventListener("DOMContentLoaded", function () {
        formEl = document.getElementById("produtoSimplesForm") || qs("form.ps-form");

        precoEl = qs('input[name="Preco"].js-money');
        promoEl = qs('input[name="PrecoPromocional"].js-money');
        promoHintEl = document.getElementById("promoHint");

        tipoEl = document.getElementById("TipoProduto") || qs('select[name="TipoProduto"]');
        nomeEl = qs('input[name="Nome"]');

        descricaoEl = document.getElementById("Descricao") || qs('textarea[name="Descricao"]');
        descricaoCounterEl = document.getElementById("descricaoCounter");

        estoqueEl = document.getElementById("Estoque") || qs('input[name="Estoque"]');
        estoqueHintEl = document.getElementById("estoqueHint");

        imgEl = document.getElementById("ImagemUpload") || qs('input[type="file"][name="ImagemUpload"]');
        imgPreviewEl = document.getElementById("imgPreview");
        imgPreviewEmptyEl = document.getElementById("imgPreviewEmpty");

        saborFieldWrap = document.getElementById("saborFieldWrap");
        podDetailsBox = document.getElementById("podDetailsBox");
        bebidaDetailsBox = document.getElementById("bebidaDetailsBox");

        saborSelectEl = document.getElementById("SaborSelect");
        saborOutroEl = document.getElementById("SaborOutro");
        saborHiddenEl = document.getElementById("Sabor");

        bebidaEmbalagemEl = document.getElementById("BebidaEmbalagem") || qs('select[name="BebidaEmbalagem"]');
        bebidaQtdPorEmbalagemEl = document.getElementById("BebidaQtdPorEmbalagem") || qs('input[name="BebidaQtdPorEmbalagem"]');
        bebidaEstoqueResumoEl = document.getElementById("BebidaEstoqueResumo");

        initMoneyFields();
        initSaborPod();
        previewImagem();
        initDescricaoCounter();

        if (tipoEl) {
            tipoEl.addEventListener("change", syncTypeBlocks);
        }

        [
            estoqueEl,
            bebidaEmbalagemEl,
            bebidaQtdPorEmbalagemEl
        ].forEach(function (el) {
            if (!el) return;

            el.addEventListener("input", updateEstoqueResumo);
            el.addEventListener("change", updateEstoqueResumo);
        });

        if (precoEl) {
            precoEl.addEventListener("input", validatePromo);
            precoEl.addEventListener("change", validatePromo);
        }

        if (promoEl) {
            promoEl.addEventListener("input", validatePromo);
            promoEl.addEventListener("change", validatePromo);
        }

        syncTypeBlocks();

        if (formEl) {
            formEl.addEventListener("submit", function (e) {
                const okPromo = validatePromo();
                const okPod = validatePodRequired();
                const okBebida = validateBebidaResumo();

                if (!okPromo || !okPod || !okBebida) {
                    e.preventDefault();
                    e.stopPropagation();
                    return false;
                }

                beforeSubmitNormalizeMoney();
            });
        }
    });
})();