// =========================================================
// Produto Simples - JS (separado)
// =========================================================

// ==== jQuery Validate: aceitar 1.234,56 como number (pt-BR) ====
(function () {
    function initValidation() {
        if (!window.jQuery || !jQuery.validator) {
            setTimeout(initValidation, 100);
            return;
        }

        jQuery.validator.methods.number = function (value, element) {
            if (this.optional(element)) return true;

            value = (value || "").toString().trim().replace(/\s/g, "");
            if (value.indexOf(",") > -1) value = value.replace(/\./g, "").replace(",", ".");
            return /^-?\d+(\.\d+)?$/.test(value);
        };

        if (jQuery.validator.methods.range) {
            var originalRange = jQuery.validator.methods.range;
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
    function qs(sel) { return document.querySelector(sel); }
    function qsa(sel) { return Array.from(document.querySelectorAll(sel)); }

    function onlyDigits(s) { return (s || "").replace(/\D+/g, ""); }

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
        if (s.includes(",") && s.includes(".")) s = s.replace(/\./g, "").replace(",", ".");
        else s = s.replace(",", ".");
        return s;
    }

    function toNumberInvariant(value) {
        const inv = normalizeMoneyToInvariant(value);
        const n = parseFloat(inv);
        return isNaN(n) ? 0 : n;
    }

    function attachMoneyMask(input, onChange) {
        function apply() {
            const digits = onlyDigits(input.value);
            input.value = formatMoneyBRFromDigits(digits);
            input.dataset.rawDigits = digits;
            onChange && onChange();
        }
        input.addEventListener("input", apply);
        input.addEventListener("blur", apply);
    }

    function setInvalid(el, on) {
        if (!el) return;
        el.classList.toggle("ps-invalid", !!on);
    }

    // elements
    let formEl;
    let precoEl, promoEl, promoHintEl;
    let tipoEl, maioridadeEl;
    let imgEl, imgPreviewEl, imgPreviewEmptyEl;

    let podSaborBox, saborSelectEl, saborOutroEl, saborHiddenEl;
    let saborSimplesBox, podDetailsBox;
    let nomeEl;

    function getEnumVals() {
        const cfg = document.getElementById("psEnums");
        const POD = cfg ? cfg.getAttribute("data-pod") : null;
        const BEBIDA = cfg ? cfg.getAttribute("data-bebida") : null;
        return { POD, BEBIDA };
    }

    function validatePromo() {
        if (!precoEl || !promoEl) return true;

        const preco = toNumberInvariant(precoEl.value);
        const promo = toNumberInvariant(promoEl.value);

        if (!promoEl.value || promo <= 0) {
            setInvalid(promoEl, false);
            if (promoHintEl) promoHintEl.textContent = "";
            return true;
        }

        if (promo >= preco) {
            setInvalid(promoEl, true);
            if (promoHintEl) promoHintEl.textContent = "Promo deve ser menor que o preço.";
            return false;
        }

        setInvalid(promoEl, false);
        if (promoHintEl) promoHintEl.textContent = "Promo válida ✅";
        return true;
    }

    function syncSaborHidden() {
        if (!saborHiddenEl) return;

        const outro = (saborOutroEl?.value || "").trim();
        const sel = (saborSelectEl?.value || "").trim();

        saborHiddenEl.value = outro.length > 0 ? outro : sel;
    }

    function initSaborPod() {
        if (!podSaborBox) return;

        if (saborSelectEl) saborSelectEl.addEventListener("change", syncSaborHidden);
        if (saborOutroEl) saborOutroEl.addEventListener("input", syncSaborHidden);

        // em edição: tenta preencher combo ou "outro"
        const current = (saborHiddenEl?.value || "").trim();
        if (current && saborSelectEl) {
            const opt = Array.from(saborSelectEl.options).find(o => (o.value || "").trim().toLowerCase() === current.toLowerCase());
            if (opt) saborSelectEl.value = opt.value;
            else if (saborOutroEl) saborOutroEl.value = current;
        }

        syncSaborHidden();
    }

    function syncNomePlaceholder() {
        if (!tipoEl || !nomeEl) return;

        const { POD, BEBIDA } = getEnumVals();
        const tipo = String(tipoEl.value || "");

        if (POD !== null && tipo === String(POD)) {
            nomeEl.placeholder = "Ex: Ignite Sex Addict 28000 puffs";
            return;
        }
        if (BEBIDA !== null && tipo === String(BEBIDA)) {
            nomeEl.placeholder = "Ex: Heineken 330ml (Unidade)";
            return;
        }
        nomeEl.placeholder = "Ex: Chocolate Lacta 90g";
    }

    function syncMaioridadeUI() {
        if (!tipoEl || !maioridadeEl) return;

        const { POD, BEBIDA } = getEnumVals();
        const tipo = String(tipoEl.value || "");

        const isPod = POD !== null && tipo === String(POD);
        const isBebida = BEBIDA !== null && tipo === String(BEBIDA);

        if (isPod || isBebida) {
            maioridadeEl.checked = true;
            maioridadeEl.disabled = true;
            const checkDiv = maioridadeEl.closest(".form-check") || maioridadeEl.closest(".produto-simples-checks div");
            if (checkDiv) checkDiv.classList.add("opacity-75");
        } else {
            maioridadeEl.disabled = false;
            const checkDiv = maioridadeEl.closest(".form-check") || maioridadeEl.closest(".produto-simples-checks div");
            if (checkDiv) checkDiv.classList.remove("opacity-75");
        }

        // Mostrar/ocultar blocos do POD
        if (podSaborBox) podSaborBox.style.display = isPod ? "block" : "none";
        if (podDetailsBox) podDetailsBox.style.display = isPod ? "block" : "none";
        if (saborSimplesBox) saborSimplesBox.style.display = isPod ? "none" : "block";

        // quando vira Pod, garantir sync do hidden
        if (isPod) syncSaborHidden();
    }

    function previewImagem() {
        if (!imgEl || !imgPreviewEl || !imgPreviewEmptyEl) return;

        imgEl.addEventListener("change", function () {
            const file = imgEl.files && imgEl.files[0];
            if (!file) return;
            if (!file.type || !file.type.startsWith("image/")) return;

            const url = URL.createObjectURL(file);
            imgPreviewEl.src = url;
            imgPreviewEl.classList.remove("d-none");
            imgPreviewEmptyEl.classList.add("d-none");
        });
    }

    function initMoneyFields() {
        qsa(".js-money").forEach(function (el) {
            attachMoneyMask(el, validatePromo);

            // formata valor inicial (se veio do server)
            if (el.value) {
                const inv = normalizeMoneyToInvariant(el.value);
                const digits = onlyDigits(inv.replace(".", ""));
                el.value = formatMoneyBRFromDigits(digits);
                el.dataset.rawDigits = digits;
            }
        });

        validatePromo();
    }

    function beforeSubmitNormalizeMoney() {
        if (precoEl) precoEl.value = normalizeMoneyToInvariant(precoEl.value);
        if (promoEl) promoEl.value = normalizeMoneyToInvariant(promoEl.value);
        syncSaborHidden();
    }

    function validatePodRequired() {
        const { POD } = getEnumVals();
        if (!tipoEl) return true;

        const tipo = String(tipoEl.value || "");
        const isPod = POD !== null && tipo === String(POD);

        if (!isPod) return true;

        // para Pod: sabor obrigatório (via hidden)
        const sabor = (saborHiddenEl?.value || "").trim();
        if (!sabor) {
            // dá um feedback simples
            if (podSaborBox) {
                const sel = saborSelectEl || saborOutroEl;
                if (sel) sel.focus();
            }
            return false;
        }
        return true;
    }

    document.addEventListener("DOMContentLoaded", function () {
        formEl = qs("form");

        precoEl = qs('input[name="Preco"].js-money');
        promoEl = qs('input[name="PrecoPromocional"].js-money');
        promoHintEl = document.getElementById("promoHint");

        tipoEl = document.getElementById("TipoProduto") || qs('select[name="TipoProduto"]');
        maioridadeEl = document.getElementById("RequerMaioridade") || qs('input[name="RequerMaioridade"]');

        imgEl = qs('input[type="file"][name="ImagemUpload"]');
        imgPreviewEl = document.getElementById("imgPreview");
        imgPreviewEmptyEl = document.getElementById("imgPreviewEmpty");

        podSaborBox = document.getElementById("podSaborBox");
        saborSelectEl = document.getElementById("SaborSelect");
        saborOutroEl = document.getElementById("SaborOutro");
        saborHiddenEl = document.getElementById("Sabor");

        saborSimplesBox = document.getElementById("saborSimplesBox");
        podDetailsBox = document.getElementById("podDetailsBox");

        nomeEl = qs('input[name="Nome"]');

        initMoneyFields();
        previewImagem();
        initSaborPod();

        if (tipoEl) {
            tipoEl.addEventListener("change", function () {
                syncMaioridadeUI();
                syncNomePlaceholder();
            });

            syncMaioridadeUI();
            syncNomePlaceholder();
        }

        if (formEl) {
            formEl.addEventListener("submit", function (e) {
                const okPromo = validatePromo();
                if (!okPromo) {
                    e.preventDefault();
                    e.stopPropagation();
                    if (promoEl) promoEl.focus();
                    return false;
                }

                // valida pod sabor obrigatório (client-side)
                if (!validatePodRequired()) {
                    e.preventDefault();
                    e.stopPropagation();
                    if (promoHintEl) {
                        // reutiliza o hint se quiser
                    }
                    return false;
                }

                beforeSubmitNormalizeMoney();
            });
        }
    });
})();