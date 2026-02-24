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

    function setInvalid(el, on) {
        if (!el) return;
        el.classList.toggle("ps-invalid", !!on);
    }

    // elements
    let formEl;
    let precoEl, promoEl, promoHintEl;

    let tipoEl, maioridadeEl;
    let nomeEl;

    let imgEl, imgPreviewEl, imgPreviewEmptyEl;

    let saborFieldWrap, corFieldWrap;
    let podSaborBox, saborSelectEl, saborOutroEl, saborHiddenEl;
    let saborSimplesBox, podDetailsBox, bebidaDetailsBox;

    let bebidaTipoEl, bebidaVolumeEl, bebidaEmbalagemEl, bebidaNomeSugestaoEl;

    function getEnumVals() {
        const cfg = document.getElementById("psEnums");
        const POD = cfg ? cfg.getAttribute("data-pod") : null;
        const BEBIDA = cfg ? cfg.getAttribute("data-bebida") : null;
        return { POD, BEBIDA };
    }

    function isTipoPod() {
        const { POD } = getEnumVals();
        return !!tipoEl && POD !== null && String(tipoEl.value || "") === String(POD);
    }

    function isTipoBebida() {
        const { BEBIDA } = getEnumVals();
        return !!tipoEl && BEBIDA !== null && String(tipoEl.value || "") === String(BEBIDA);
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

        // Se for bebida, não usa sabor
        if (isTipoBebida()) {
            saborHiddenEl.value = "";
            return;
        }

        const outro = (saborOutroEl?.value || "").trim();
        const sel = (saborSelectEl?.value || "").trim();
        const simples = qs('input[name="Sabor"]:not([type="hidden"])');

        if (isTipoPod()) {
            saborHiddenEl.value = outro.length > 0 ? outro : sel;
        } else {
            // Padrão -> campo simples
            if (simples) saborHiddenEl.value = (simples.value || "").trim();
        }
    }

    function initSaborPod() {
        if (saborSelectEl) saborSelectEl.addEventListener("change", syncSaborHidden);
        if (saborOutroEl) saborOutroEl.addEventListener("input", syncSaborHidden);

        const saborSimplesInput = qs('#saborSimplesBox input[name="Sabor"]');
        if (saborSimplesInput) saborSimplesInput.addEventListener("input", syncSaborHidden);

        // em edição: tenta preencher combo ou "outro" (quando for pod)
        const current = (saborHiddenEl?.value || "").trim();
        if (current && saborSelectEl) {
            const opt = Array.from(saborSelectEl.options)
                .find(o => (o.value || "").trim().toLowerCase() === current.toLowerCase());

            if (opt) saborSelectEl.value = opt.value;
            else if (saborOutroEl) saborOutroEl.value = current;
        }

        syncSaborHidden();
    }

    function syncNomePlaceholder() {
        if (!tipoEl || !nomeEl) return;

        if (isTipoPod()) {
            nomeEl.placeholder = "Ex: Ignite Sex Addict 28000 puffs";
            return;
        }

        if (isTipoBebida()) {
            nomeEl.placeholder = "Ex: Heineken 330ml (Unidade)";
            return;
        }

        nomeEl.placeholder = "Ex: Chocolate Lacta 90g";
    }

    function syncMaioridadeUI() {
        if (!maioridadeEl) return;

        const pod = isTipoPod();
        const bebida = isTipoBebida();

        if (pod || bebida) {
            maioridadeEl.checked = true;
            maioridadeEl.disabled = true;
        } else {
            maioridadeEl.disabled = false;
        }
    }

    function syncTypeBlocks() {
        const pod = isTipoPod();
        const bebida = isTipoBebida();

        // wrappers gerais
        if (saborFieldWrap) saborFieldWrap.style.display = bebida ? "none" : "";
        if (corFieldWrap) corFieldWrap.style.display = bebida ? "none" : "";

        // dentro do sabor
        if (podSaborBox) podSaborBox.style.display = pod ? "block" : "none";
        if (saborSimplesBox) saborSimplesBox.style.display = (!pod && !bebida) ? "block" : "none";

        // cards detalhes
        if (podDetailsBox) podDetailsBox.style.display = pod ? "block" : "none";
        if (bebidaDetailsBox) bebidaDetailsBox.style.display = bebida ? "block" : "none";

        // bebida não usa sabor/cor
        if (bebida) {
            if (saborHiddenEl) saborHiddenEl.value = "";
            const saborSimplesInput = qs('#saborSimplesBox input[name="Sabor"]');
            if (saborSimplesInput) saborSimplesInput.value = "";

            if (saborSelectEl) saborSelectEl.value = "";
            if (saborOutroEl) saborOutroEl.value = "";

            const corInput = qs('input[name="Cor"]');
            if (corInput) corInput.value = "";
        }

        syncSaborHidden();
        syncMaioridadeUI();
        syncNomePlaceholder();
        updateBebidaNomeSugestao();
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

            if (el.value) {
                const inv = normalizeMoneyToInvariant(el.value);
                const digits = onlyDigits(inv.replace(".", ""));
                el.value = formatMoneyBRFromDigits(digits);
                el.dataset.rawDigits = digits;
            }
        });

        validatePromo();
    }

    function initAlcoolField() {
        const alcoolEl = qs(".js-alcool");
        if (!alcoolEl) return;

        alcoolEl.addEventListener("input", function () {
            let v = alcoolEl.value || "";
            v = v.replace(/[^\d,.\-]/g, "");

            // normaliza para vírgula visual
            if (v.includes(".") && !v.includes(",")) v = v.replace(".", ",");

            // mantém apenas a primeira vírgula
            const firstComma = v.indexOf(",");
            if (firstComma >= 0) {
                v = v.slice(0, firstComma + 1) + v.slice(firstComma + 1).replace(/,/g, "");
            }

            alcoolEl.value = v;
        });
    }

    function updateBebidaNomeSugestao() {
        if (!bebidaNomeSugestaoEl) return;

        if (!isTipoBebida()) {
            bebidaNomeSugestaoEl.value = "";
            return;
        }

        const nome = (nomeEl?.value || "").trim();
        const volume = (bebidaVolumeEl?.value || "").trim();
        const embalagemTxt = bebidaEmbalagemEl
            ? (bebidaEmbalagemEl.options[bebidaEmbalagemEl.selectedIndex]?.text || "").trim()
            : "";

        let partes = [];
        if (nome) partes.push(nome);
        if (volume) partes.push(`${volume}ml`);

        if (embalagemTxt && embalagemTxt.toLowerCase() !== "não informado") {
            partes.push(embalagemTxt);
        }

        bebidaNomeSugestaoEl.value = partes.join(" ").replace(/\s+/g, " ").trim();
    }

    function beforeSubmitNormalizeMoney() {
        if (precoEl) precoEl.value = normalizeMoneyToInvariant(precoEl.value);
        if (promoEl) promoEl.value = normalizeMoneyToInvariant(promoEl.value);

        // teor alcoólico -> normaliza vírgula para ponto
        const alcoolEl = qs(".js-alcool");
        if (alcoolEl && alcoolEl.value) {
            alcoolEl.value = normalizeMoneyToInvariant(alcoolEl.value);
        }

        syncSaborHidden();
    }

    function validatePodRequired() {
        if (!isTipoPod()) return true;

        const sabor = (saborHiddenEl?.value || "").trim();
        if (!sabor) {
            const foco = saborSelectEl || saborOutroEl;
            if (foco) foco.focus();
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
        nomeEl = qs('input[name="Nome"]');

        imgEl = qs('input[type="file"][name="ImagemUpload"]');
        imgPreviewEl = document.getElementById("imgPreview");
        imgPreviewEmptyEl = document.getElementById("imgPreviewEmpty");

        saborFieldWrap = document.getElementById("saborFieldWrap");
        corFieldWrap = document.getElementById("corFieldWrap");

        podSaborBox = document.getElementById("podSaborBox");
        saborSelectEl = document.getElementById("SaborSelect");
        saborOutroEl = document.getElementById("SaborOutro");
        saborHiddenEl = document.getElementById("Sabor");

        saborSimplesBox = document.getElementById("saborSimplesBox");
        podDetailsBox = document.getElementById("podDetailsBox");
        bebidaDetailsBox = document.getElementById("bebidaDetailsBox");

        bebidaTipoEl = qs('input[name="BebidaTipo"]');
        bebidaVolumeEl = qs('input[name="BebidaVolumeMl"]');
        bebidaEmbalagemEl = document.getElementById("BebidaEmbalagem") || qs('select[name="BebidaEmbalagem"]');
        bebidaNomeSugestaoEl = document.getElementById("BebidaNomeSugestao");

        initMoneyFields();
        initAlcoolField();
        previewImagem();
        initSaborPod();

        [nomeEl, bebidaTipoEl, bebidaVolumeEl, bebidaEmbalagemEl].forEach(function (el) {
            if (!el) return;
            el.addEventListener("input", updateBebidaNomeSugestao);
            el.addEventListener("change", updateBebidaNomeSugestao);
        });

        if (tipoEl) {
            tipoEl.addEventListener("change", function () {
                syncTypeBlocks();
            });
        }

        // estado inicial
        syncTypeBlocks();

        if (formEl) {
            formEl.addEventListener("submit", function (e) {
                const okPromo = validatePromo();
                if (!okPromo) {
                    e.preventDefault();
                    e.stopPropagation();
                    if (promoEl) promoEl.focus();
                    return false;
                }

                if (!validatePodRequired()) {
                    e.preventDefault();
                    e.stopPropagation();
                    return false;
                }

                beforeSubmitNormalizeMoney();
            });
        }
    });
})();