document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('productForm');
    const variationsList = document.getElementById('variationsList');

    const totalStockEl = document.getElementById('totalStock');
    const totalStockInput = document.getElementById('totalStockInput');

    const btnAdd = document.getElementById('addVariation');
    const btnClear = document.getElementById('clearAllVariations');

    const progressText = document.getElementById('progressText');
    const progressBar = document.getElementById('progressBar');

    const desc = document.querySelector('[name="Descricao"]');
    const descCount = document.getElementById('descCount');
    const nome = document.querySelector('[name="Nome"]');
    const nomeOk = document.getElementById('nomeOk');
    const categoria = document.querySelector('[name="CategoriaId"]');

    const tipoRadios = Array.from(document.querySelectorAll('input[name="TipoProdutoUI"]'));
    const typeCards = Array.from(document.querySelectorAll('.pf2-type-item'));
    const maioridade = document.getElementById('requerMaioridade');
    const maioridadeHint = document.getElementById('maioridadeHint');
    const maioridadeCard = document.getElementById('maioridadeCard');

    // ✅ hidden REAL (vai pro banco) - enum int: 0,1,2
    const tipoHidden = document.getElementById('TipoProdutoHidden');

    const imageUpload = document.getElementById('imageUpload');
    const imagePreviewBox = document.getElementById('imagePreviewBox');

    const toastHost = document.getElementById('pf2ToastHost');

    const idHidden = document.querySelector('input[name="Id"]');
    const draftKey = `produto-rascunho:${(idHidden && idHidden.value && idHidden.value !== "0") ? idHidden.value : "new"}`;

    if (!variationsList || !btnAdd) return;

    // ✅ Tipo travado no Editar (quando você desabilita os radios no servidor)
    const typeIsLocked = tipoRadios.some(r => r.disabled);

    /* =========================
       Toast
       ========================= */
    const toast = (type, title, desc) => {
        if (!toastHost) return;

        const iconByType = {
            info: 'fa-circle-info',
            ok: 'fa-circle-check',
            warn: 'fa-triangle-exclamation',
            err: 'fa-circle-xmark'
        };

        const node = document.createElement('div');
        node.className = `pf2-toast is-${type}`;
        node.innerHTML = `
      <div class="ico"><i class="fas ${iconByType[type] || iconByType.info}"></i></div>
      <div>
        <div class="title">${title || ''}</div>
        ${desc ? `<div class="desc">${desc}</div>` : ``}
      </div>
    `;

        toastHost.appendChild(node);

        setTimeout(() => {
            node.style.animation = 'pf2ToastOut .22s ease forwards';
            setTimeout(() => node.remove(), 260);
        }, 2200);
    };

    /* =========================
       Money helpers (pt-BR) - LIVE
       ========================= */
    const moneyFromDigits = (digits) => {
        const n = Number(digits || 0) / 100;
        return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    };

    const onlyDigits = (s) => (s || '').replace(/\D/g, '');
    const normalizeMoney = (val) => moneyFromDigits(onlyDigits(val));

    const maskMoneyLive = (input) => {
        const digits = onlyDigits(input.value);
        const limited = digits.length > 12 ? digits.slice(0, 12) : digits;
        input.value = moneyFromDigits(limited);
        const len = input.value.length;
        try { input.setSelectionRange(len, len); } catch { }
    };

    const wireMoney = (input) => {
        if (!input) return;

        input.addEventListener('focus', () => {
            if (!input.value || input.value.trim() === '') input.value = '0,00';
            const len = input.value.length;
            try { input.setSelectionRange(len, len); } catch { }
        });

        input.addEventListener('input', () => {
            maskMoneyLive(input);
            scheduleDraftSave();
        });

        input.addEventListener('blur', () => {
            input.value = normalizeMoney(input.value);
            scheduleDraftSave();
        });
    };

    /* =========================
       Tipo Produto UI (salva no hidden int)
       enum int: Padrao=0, PodVape=1, BebidaAlcoolica=2
       ========================= */
    const uiToEnumInt = (ui) => {
        if (ui === 'pod') return '1';
        if (ui === 'bebida-alcoolica') return '2';
        return '0';
    };

    const enumIntToUi = (val) => {
        const v = String(val ?? '0');
        if (v === '1') return 'pod';
        if (v === '2') return 'bebida-alcoolica';
        return 'padrao';
    };

    const getType = () => {
        const checked = tipoRadios.find(r => r.checked);
        return checked ? checked.value : 'padrao';
    };

    const setType = (uiValue, saveDraft = false) => {
        const radio = tipoRadios.find(r => r.value === uiValue);
        if (radio) radio.checked = true;
        applyTypeUI(saveDraft);
    };

    const applyTypeUI = (saveDraft = true) => {
        const t = getType();

        typeCards.forEach(card => {
            card.classList.toggle('is-selected', card.dataset.type === t);
        });

        // ✅ atualiza hidden real (vai pro banco)
        if (tipoHidden) tipoHidden.value = uiToEnumInt(t);

        // maioridade: força para Pod/Bebida
        if (maioridade) {
            const forced = (t === 'pod' || t === 'bebida-alcoolica');
            maioridade.disabled = forced;
            if (forced) maioridade.checked = true;

            if (maioridadeHint) {
                maioridadeHint.textContent = forced
                    ? 'Obrigatório para este tipo de produto'
                    : 'Venda apenas para maiores de 18 anos';
            }
            if (maioridadeCard) maioridadeCard.style.opacity = forced ? '0.92' : '1';
        }

        updateProgress();
        if (saveDraft) scheduleDraftSave();
    };

    // ✅ força o UI pelo hidden do banco (Edit e Create)
    const forceTypeFromHidden = () => {
        if (!tipoHidden) return;
        const ui = enumIntToUi(tipoHidden.value);
        setType(ui, false); // não salva draft aqui
    };

    /* =========================
       Variation helpers
       ========================= */
    const syncName = (card) => {
        const sel = card.querySelector('.v-name');
        const custom = card.querySelector('.v-custom');
        const hidden = card.querySelector('.v-name-hidden');
        const badge = card.querySelector('.v-badge');

        if (!sel || !custom || !hidden) return;

        if (sel.value === 'Outro') {
            custom.classList.remove('hidden');
            hidden.value = (custom.value || '').trim();
        } else {
            custom.classList.add('hidden');
            custom.value = '';
            hidden.value = sel.value;
        }

        if (badge) badge.textContent = hidden.value || 'Variação';
    };

    const calcTotalStock = () => {
        let total = 0;

        variationsList.querySelectorAll('.pf2-vari').forEach(card => {
            const stock = parseInt(card.querySelector('.v-stock')?.value, 10) || 0;
            const mult = parseInt(card.querySelector('.v-mult')?.value, 10) || 1;
            const active = card.querySelector('.v-active')?.checked ?? true;

            if (active) total += stock * mult;
        });

        const txt = total.toLocaleString('pt-BR');
        if (totalStockEl) totalStockEl.textContent = txt;
        if (totalStockInput) totalStockInput.value = txt;

        return total;
    };

    const renumber = () => {
        const cards = Array.from(variationsList.querySelectorAll('.pf2-vari'));
        cards.forEach((card, idx) => {
            card.dataset.index = idx;

            card.querySelectorAll('[name]').forEach(el => {
                const nameAttr = el.getAttribute('name');
                if (!nameAttr) return;
                el.setAttribute('name', nameAttr.replace(/Variacoes\[\d+\]/g, `Variacoes[${idx}]`));
            });
        });
    };

    const validateVariationCard = (card) => {
        syncName(card);

        const nomeHidden = (card.querySelector('.v-name-hidden')?.value || '').trim();
        const price = (card.querySelector('.v-price')?.value || '').trim();

        const nomeVal = card.querySelector('.v-nome-val');
        const precoVal = card.querySelector('.v-preco-val');

        const nomeOkNow = !!nomeHidden;
        const precoOkNow = !!price && normalizeMoney(price) !== '0,00';

        if (nomeVal) nomeVal.classList.toggle('hidden', nomeOkNow);
        if (precoVal) precoVal.classList.toggle('hidden', precoOkNow);

        return nomeOkNow && precoOkNow;
    };

    const updateProgress = () => {
        if (!progressText || !progressBar) return;

        let done = 0;
        const total = 3;

        const nomeOkNow = (nome?.value || '').trim().length > 0;
        if (nomeOkNow) done++;

        const catOkNow = (categoria?.value || '').toString().trim().length > 0 && categoria.value !== "0";
        if (catOkNow) done++;

        const hasValidVar = Array.from(variationsList.querySelectorAll('.pf2-vari')).some(validateVariationCard);
        if (hasValidVar) done++;

        const pct = Math.round((done / total) * 100);
        progressText.textContent = `${pct}%`;
        progressBar.style.width = `${pct}%`;

        if (nomeOk) nomeOk.style.display = nomeOkNow ? 'block' : 'none';
    };

    const wireVariation = (card) => {
        const sel = card.querySelector('.v-name');
        const custom = card.querySelector('.v-custom');
        const mult = card.querySelector('.v-mult');
        const price = card.querySelector('.v-price');
        const promo = card.querySelector('.v-promo');
        const stock = card.querySelector('.v-stock');
        const active = card.querySelector('.v-active');
        const remove = card.querySelector('.v-remove');

        const onAnyChange = () => {
            validateVariationCard(card);
            calcTotalStock();
            updateProgress();
            scheduleDraftSave();
        };

        if (sel) sel.addEventListener('change', onAnyChange);
        if (custom) custom.addEventListener('input', onAnyChange);

        wireMoney(price);
        wireMoney(promo);

        if (mult) mult.addEventListener('input', onAnyChange);
        if (stock) stock.addEventListener('input', onAnyChange);
        if (active) active.addEventListener('change', onAnyChange);

        if (remove) {
            remove.addEventListener('click', () => {
                const count = variationsList.querySelectorAll('.pf2-vari').length;
                if (count <= 1) {
                    toast('err', 'Erro', 'O produto precisa ter pelo menos uma variação.');
                    return;
                }
                card.remove();
                renumber();
                calcTotalStock();
                updateProgress();
                scheduleDraftSave();
                toast('info', 'Variação removida');
            });
        }

        syncName(card);
        validateVariationCard(card);
    };

    const createVariationCard = (idx, data) => {
        const d = data || {
            id: '',
            nome: 'Unidade',
            multiplicador: 1,
            precoTexto: '',
            precoPromocionalTexto: '',
            estoque: 0,
            ativo: true
        };

        const isPadrao = d.nome === 'Unidade' || d.nome === 'Caixa' || d.nome === 'Fardo';
        const isOutro = !isPadrao;

        const div = document.createElement('div');
        div.className = 'pf2-vari';
        div.dataset.index = idx;

        div.innerHTML = `
      <input type="hidden" name="Variacoes[${idx}].Id" value="${d.id ?? ''}" />
      <input type="hidden" class="v-name-hidden" name="Variacoes[${idx}].Nome" value="${d.nome ?? 'Unidade'}" />

      <div class="pf2-vari-top">
        <div class="pf2-vari-badge">
          <i class="fas fa-cubes"></i>
          <span>Variação</span>
          <strong class="v-badge">${d.nome ?? 'Unidade'}</strong>
        </div>

        <button type="button" class="pf2-iconbtn v-remove" title="Remover">
          <i class="fas fa-trash"></i>
        </button>
      </div>

      <div class="pf2-vari-grid">
        <div class="pf2-field">
          <label>Nome</label>
          <div class="pf2-row">
            <select class="v-name">
              <option value="Unidade" ${d.nome === 'Unidade' ? 'selected' : ''}>Unidade</option>
              <option value="Caixa" ${d.nome === 'Caixa' ? 'selected' : ''}>Caixa</option>
              <option value="Fardo" ${d.nome === 'Fardo' ? 'selected' : ''}>Fardo</option>
              <option value="Outro" ${isOutro ? 'selected' : ''}>Outro</option>
            </select>
            <input type="text" class="v-custom ${isOutro ? '' : 'hidden'}" placeholder="Nome..." value="${isOutro ? (d.nome ?? '') : ''}" />
          </div>
          <span class="pf2-val v-nome-val hidden">Informe o nome da variação.</span>
        </div>

        <div class="pf2-field pf2-inline2">
          <div class="pf2-inline2-col">
            <label>Multiplicador</label>
            <input type="number" min="1" class="v-mult" name="Variacoes[${idx}].Multiplicador" value="${Math.max(1, parseInt(d.multiplicador ?? 1, 10) || 1)}" />
          </div>
          <div class="pf2-inline2-col">
            <label>Estoque</label>
            <input type="number" min="0" class="v-stock" name="Variacoes[${idx}].Estoque" value="${parseInt(d.estoque ?? 0, 10) || 0}" />
          </div>
        </div>

        <div class="pf2-field">
          <label>Preço *</label>
          <div class="pf2-money">
            <span>R$</span>
            <input type="text" class="v-price" name="Variacoes[${idx}].PrecoTexto" placeholder="0,00" value="${d.precoTexto ?? ''}" />
          </div>
          <span class="pf2-val v-preco-val hidden">Informe o preço.</span>
        </div>

        <div class="pf2-field">
          <label>Promo</label>
          <div class="pf2-money">
            <span>R$</span>
            <input type="text" class="v-promo" name="Variacoes[${idx}].PrecoPromocionalTexto" placeholder="0,00" value="${d.precoPromocionalTexto ?? ''}" />
          </div>
        </div>

        <div class="pf2-field pf2-switchfield">
          <label>Ativo</label>
          <div class="pf2-switch">
            <input type="checkbox" class="v-active" name="Variacoes[${idx}].Ativo" value="true" ${d.ativo ? 'checked' : ''} />
            <span></span>
          </div>
          <input type="hidden" name="Variacoes[${idx}].Ativo" value="false" />
        </div>
      </div>
    `;

        return div;
    };

    /* =========================
       Draft
       ========================= */
    let saveTimer = null;

    const buildDraft = async () => {
        const draft = {
            tipoProdutoUI: getType(),
            tipoProdutoHidden: tipoHidden?.value ?? '0',
            produto: {
                nome: nome?.value ?? '',
                descricao: desc?.value ?? '',
                categoriaId: categoria?.value ?? '',
                marca: document.querySelector('[name="Marca"]')?.value ?? '',
                sku: document.querySelector('[name="SKU"]')?.value ?? '',
                codigoBarras: document.querySelector('[name="CodigoBarras"]')?.value ?? '',
                ativo: document.querySelector('[name="Ativo"]')?.checked ?? true,
                emPromocao: document.querySelector('[name="EmPromocao"]')?.checked ?? false,
                maisVendido: document.querySelector('[name="MaisVendido"]')?.checked ?? false,
                requerMaioridade: document.querySelector('[name="RequerMaioridade"]')?.checked ?? false
            },
            variacoes: Array.from(variationsList.querySelectorAll('.pf2-vari')).map(card => {
                syncName(card);
                return {
                    id: card.querySelector('input[name$=".Id"]')?.value ?? '',
                    nome: card.querySelector('.v-name-hidden')?.value ?? '',
                    multiplicador: card.querySelector('.v-mult')?.value ?? '1',
                    precoTexto: card.querySelector('.v-price')?.value ?? '',
                    precoPromocionalTexto: card.querySelector('.v-promo')?.value ?? '',
                    estoque: card.querySelector('.v-stock')?.value ?? '0',
                    ativo: card.querySelector('.v-active')?.checked ?? true
                };
            }),
            imagePreviewBase64: null
        };

        const file = imageUpload?.files?.[0];
        if (file && file.type.startsWith('image/')) {
            draft.imagePreviewBase64 = await new Promise((resolve) => {
                const reader = new FileReader();
                reader.onload = (ev) => resolve(ev.target.result);
                reader.readAsDataURL(file);
            });
        } else {
            const img = imagePreviewBox?.querySelector('img');
            const src = img?.getAttribute('src') || '';
            draft.imagePreviewBase64 = src.startsWith('data:image/') ? src : null;
        }

        return draft;
    };

    const scheduleDraftSave = () => {
        if (saveTimer) clearTimeout(saveTimer);
        saveTimer = setTimeout(async () => {
            try {
                const draft = await buildDraft();
                const hasAny =
                    (draft.produto.nome || '').trim().length > 0 ||
                    draft.variacoes.some(v => (v.precoTexto || '').trim().length > 0);

                if (!hasAny) {
                    localStorage.removeItem(draftKey);
                    return;
                }

                localStorage.setItem(draftKey, JSON.stringify(draft));
            } catch (e) {
                console.error('Draft save error', e);
            }
        }, 700);
    };

    const loadDraft = () => {
        const raw = localStorage.getItem(draftKey);
        if (!raw) return false;

        try {
            const d = JSON.parse(raw);

            if (nome) nome.value = d?.produto?.nome ?? '';
            if (desc) desc.value = d?.produto?.descricao ?? '';
            if (categoria) categoria.value = d?.produto?.categoriaId ?? '';

            const marca = document.querySelector('[name="Marca"]');
            const sku = document.querySelector('[name="SKU"]');
            const cb = document.querySelector('[name="CodigoBarras"]');

            if (marca) marca.value = d?.produto?.marca ?? '';
            if (sku) sku.value = d?.produto?.sku ?? '';
            if (cb) cb.value = d?.produto?.codigoBarras ?? '';

            const ativo = document.querySelector('[name="Ativo"]');
            const emPromo = document.querySelector('[name="EmPromocao"]');
            const maisVend = document.querySelector('[name="MaisVendido"]');
            const reqMaior = document.querySelector('[name="RequerMaioridade"]');

            if (ativo) ativo.checked = !!d?.produto?.ativo;
            if (emPromo) emPromo.checked = !!d?.produto?.emPromocao;
            if (maisVend) maisVend.checked = !!d?.produto?.maisVendido;
            if (reqMaior) reqMaior.checked = !!d?.produto?.requerMaioridade;

            // ✅ TIPO:
            // - Editar travado: NÃO deixa draft mexer no tipo
            // - Criar: pode restaurar
            if (!typeIsLocked) {
                if (tipoHidden && d?.tipoProdutoHidden != null) tipoHidden.value = String(d.tipoProdutoHidden);
                if (d?.tipoProdutoUI) setType(d.tipoProdutoUI, false);
            }

            // variações
            variationsList.innerHTML = '';
            const vars = Array.isArray(d?.variacoes) && d.variacoes.length ? d.variacoes : [null];
            vars.forEach((v, idx) => {
                const card = createVariationCard(idx, v || undefined);
                variationsList.appendChild(card);
                wireVariation(card);
            });
            renumber();

            // preview
            if (d?.imagePreviewBase64 && typeof d.imagePreviewBase64 === 'string' && d.imagePreviewBase64.startsWith('data:image/')) {
                if (imagePreviewBox) {
                    imagePreviewBox.innerHTML = `
            <img src="${d.imagePreviewBase64}" alt="Preview" />
            <button type="button" class="pf2-x" id="removeImage"><i class="fas fa-times"></i></button>
          `;
                    document.getElementById('removeImage')?.addEventListener('click', () => {
                        if (imagePreviewBox) imagePreviewBox.innerHTML = `
              <div class="pf2-empty">
                <i class="fas fa-image"></i>
                <span class="muted">Nenhuma imagem selecionada</span>
              </div>`;
                        if (imageUpload) imageUpload.value = '';
                        scheduleDraftSave();
                    });
                }
            }

            return true;
        } catch (e) {
            console.error('Draft load error', e);
            return false;
        }
    };

    const clearDraft = () => localStorage.removeItem(draftKey);

    /* =========================
       Core listeners
       ========================= */
    btnAdd.addEventListener('click', () => {
        const idx = variationsList.querySelectorAll('.pf2-vari').length;
        const card = createVariationCard(idx);
        variationsList.appendChild(card);
        wireVariation(card);
        renumber();
        calcTotalStock();
        updateProgress();
        scheduleDraftSave();
        toast('ok', 'Variação adicionada');
    });

    if (btnClear) {
        btnClear.addEventListener('click', () => {
            if (!confirm('Tem certeza que deseja excluir TODAS as variações?')) return;

            variationsList.innerHTML = '';
            const first = createVariationCard(0);
            variationsList.appendChild(first);
            wireVariation(first);

            renumber();
            calcTotalStock();
            updateProgress();
            scheduleDraftSave();
            toast('warn', 'Variações limpas', 'Mantivemos 1 variação (obrigatório)');
        });
    }

    if (imageUpload) {
        imageUpload.addEventListener('change', (e) => {
            const file = e.target.files?.[0];
            if (!file) return;

            if (file.size > 5 * 1024 * 1024) {
                toast('err', 'Imagem muito grande', 'O arquivo deve ter no máximo 5MB');
                imageUpload.value = '';
                return;
            }
            if (!file.type.startsWith('image/')) {
                toast('err', 'Arquivo inválido', 'Selecione uma imagem válida');
                imageUpload.value = '';
                return;
            }

            const reader = new FileReader();
            reader.onload = (ev) => {
                if (imagePreviewBox) {
                    imagePreviewBox.innerHTML = `
            <img src="${ev.target.result}" alt="Preview" />
            <button type="button" class="pf2-x" id="removeImage"><i class="fas fa-times"></i></button>
          `;
                    document.getElementById('removeImage')?.addEventListener('click', () => {
                        if (imagePreviewBox) imagePreviewBox.innerHTML = `
              <div class="pf2-empty">
                <i class="fas fa-image"></i>
                <span class="muted">Nenhuma imagem selecionada</span>
              </div>`;
                        imageUpload.value = '';
                        scheduleDraftSave();
                    });
                }
                scheduleDraftSave();
            };
            reader.readAsDataURL(file);
        });
    }

    const updateDescCount = () => {
        if (!desc || !descCount) return;
        descCount.textContent = String((desc.value || '').length);
    };

    if (desc) desc.addEventListener('input', () => { updateDescCount(); scheduleDraftSave(); });
    if (nome) nome.addEventListener('input', () => { updateProgress(); scheduleDraftSave(); });
    if (categoria) categoria.addEventListener('change', () => { updateProgress(); scheduleDraftSave(); });

    ['Ativo', 'EmPromocao', 'MaisVendido', 'RequerMaioridade'].forEach(n => {
        const el = document.querySelector(`[name="${n}"]`);
        if (el) el.addEventListener('change', () => { updateProgress(); scheduleDraftSave(); });
    });

    // ✅ Tipo Produto: só no Criar
    if (!typeIsLocked) {
        tipoRadios.forEach(r => r.addEventListener('change', () => applyTypeUI(true)));
        typeCards.forEach(card => {
            card.addEventListener('click', () => {
                const t = card.dataset.type;
                const radio = tipoRadios.find(r => r.value === t);
                if (radio) {
                    radio.checked = true;
                    applyTypeUI(true);
                }
            });
        });
    }

    // submit
    if (form) {
        form.addEventListener('submit', (e) => {
            let ok = true;

            variationsList.querySelectorAll('.pf2-vari').forEach(card => {
                syncName(card);

                const p = card.querySelector('.v-price');
                const pr = card.querySelector('.v-promo');
                if (p) p.value = normalizeMoney(p.value);
                if (pr) pr.value = normalizeMoney(pr.value);

                if (!validateVariationCard(card)) ok = false;
            });

            calcTotalStock();
            updateProgress();

            if (!ok) {
                e.preventDefault();
                toast('err', 'Erro no formulário', 'Verifique as variações: Nome e Preço são obrigatórios');
                return;
            }

            clearDraft();
            toast('ok', 'Enviando', 'Salvando produto...');
        });
    }

    /* =========================
       INIT (ordem correta)
       1) carrega draft
       2) se não carregou, wire html
       3) força tipo pelo hidden (sempre)
       ========================= */
    const loaded = loadDraft();

    if (!loaded) {
        variationsList.querySelectorAll('.pf2-vari').forEach(wireVariation);
        renumber();
    }

    // money nos inputs do server
    variationsList.querySelectorAll('.v-price').forEach(wireMoney);
    variationsList.querySelectorAll('.v-promo').forEach(wireMoney);

    updateDescCount();

    // ✅ Garantia final: no Editar o tipo sempre reflete o banco
    forceTypeFromHidden();

    calcTotalStock();
    updateProgress();
});
