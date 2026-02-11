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

    // image
    const imageUpload = document.getElementById('imageUpload');
    const imagePreviewBox = document.getElementById('imagePreviewBox');

    // toast host
    const toastHost = document.getElementById('pf2ToastHost');

    // key do rascunho (novo vs editar)
    const idHidden = document.querySelector('input[name="Id"]');
    const draftKey = `produto-rascunho:${(idHidden && idHidden.value && idHidden.value !== "0") ? idHidden.value : "new"}`;

    if (!variationsList || !btnAdd) return;

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
        }, 2600);
    };

    /* =========================
       Money helpers
       ========================= */
    const moneyFormatFromDigits = (digits) => {
        const n = Number(digits || 0) / 100;
        return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    };

    const normalizeMoney = (val) => {
        const digits = (val || '').replace(/\D/g, '');
        return moneyFormatFromDigits(digits);
    };

    const maskMoneyOnBlur = (input) => {
        input.value = normalizeMoney(input.value);
    };

    /* =========================
       Tipo Produto UI
       ========================= */
    const getType = () => {
        const checked = tipoRadios.find(r => r.checked);
        return checked ? checked.value : 'padrao';
    };

    const setType = (value) => {
        const radio = tipoRadios.find(r => r.value === value);
        if (radio) radio.checked = true;
        applyTypeUI();
    };

    const applyTypeUI = () => {
        const t = getType();

        typeCards.forEach(card => {
            card.classList.toggle('is-selected', card.dataset.type === t);
        });

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
        scheduleDraftSave();
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

        if (price) {
            price.addEventListener('blur', () => { maskMoneyOnBlur(price); onAnyChange(); });
            price.addEventListener('input', scheduleDraftSave);
        }
        if (promo) promo.addEventListener('blur', () => { maskMoneyOnBlur(promo); scheduleDraftSave(); });

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

        <div class="pf2-field">
          <label>Multiplicador</label>
          <input type="number" min="1" class="v-mult" name="Variacoes[${idx}].Multiplicador" value="${Math.max(1, parseInt(d.multiplicador ?? 1, 10) || 1)}" />
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

        <div class="pf2-field">
          <label>Estoque</label>
          <input type="number" min="0" class="v-stock" name="Variacoes[${idx}].Estoque" value="${parseInt(d.estoque ?? 0, 10) || 0}" />
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
       Image preview helpers + salvar preview no draft
       ========================= */
    const renderEmptyPreview = () => {
        if (!imagePreviewBox) return;
        imagePreviewBox.innerHTML = `
      <div class="pf2-empty">
        <i class="fas fa-image"></i>
        <span class="muted">Nenhuma imagem selecionada</span>
      </div>
    `;
    };

    const renderImagePreview = (src, fromDraft) => {
        if (!imagePreviewBox) return;
        imagePreviewBox.innerHTML = `
      <img src="${src}" alt="Preview" />
      <button type="button" class="pf2-x" id="removeImage"><i class="fas fa-times"></i></button>
    `;
        document.getElementById('removeImage')?.addEventListener('click', () => {
            renderEmptyPreview();
            if (imageUpload) imageUpload.value = '';
            // remove preview do draft
            scheduleDraftSave(true);
            toast('info', 'Imagem removida');
        });

        if (!fromDraft) toast('ok', 'Imagem carregada');
    };

    /* =========================
       Draft: save / load
       ========================= */
    let saveTimer = null;

    const buildDraft = async (forceRemoveImagePreview) => {
        const draft = {
            tipoProdutoUI: getType(),
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

        // preview base64 (apenas se tiver arquivo selecionado)
        if (forceRemoveImagePreview) {
            draft.imagePreviewBase64 = null;
            return draft;
        }

        const file = imageUpload?.files?.[0];
        if (file && file.type.startsWith('image/')) {
            draft.imagePreviewBase64 = await new Promise((resolve) => {
                const reader = new FileReader();
                reader.onload = (ev) => resolve(ev.target.result);
                reader.readAsDataURL(file);
            });
        } else {
            // se não tem file, tenta manter preview atual se for base64 já renderizada (ex.: restore)
            const img = imagePreviewBox?.querySelector('img');
            const src = img?.getAttribute('src') || '';
            draft.imagePreviewBase64 = src.startsWith('data:image/') ? src : null;
        }

        return draft;
    };

    const scheduleDraftSave = (forceRemoveImagePreview) => {
        if (saveTimer) clearTimeout(saveTimer);
        saveTimer = setTimeout(async () => {
            try {
                const draft = await buildDraft(!!forceRemoveImagePreview);

                // não salva se estiver totalmente vazio (nome vazio e sem preço em variações)
                const hasAny =
                    (draft.produto.nome || '').trim().length > 0 ||
                    draft.variacoes.some(v => (v.precoTexto || '').trim().length > 0);

                if (!hasAny) {
                    localStorage.removeItem(draftKey);
                    return;
                }

                localStorage.setItem(draftKey, JSON.stringify(draft));
                toast('info', 'Rascunho salvo', 'Salvo automaticamente');
            } catch (e) {
                // silencioso
                console.error('Draft save error', e);
            }
        }, 900);
    };

    const loadDraft = () => {
        const raw = localStorage.getItem(draftKey);
        if (!raw) return false;

        try {
            const d = JSON.parse(raw);

            // produto
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

            // tipo UI
            if (d?.tipoProdutoUI) setType(d.tipoProdutoUI);

            // variações (recria)
            variationsList.innerHTML = '';
            const vars = Array.isArray(d?.variacoes) && d.variacoes.length ? d.variacoes : [null];
            vars.forEach((v, idx) => {
                const card = createVariationCard(idx, v || undefined);
                variationsList.appendChild(card);
                wireVariation(card);
            });
            renumber();

            // preview image
            if (d?.imagePreviewBase64 && typeof d.imagePreviewBase64 === 'string' && d.imagePreviewBase64.startsWith('data:image/')) {
                renderImagePreview(d.imagePreviewBase64, true);
            }

            toast('ok', 'Rascunho carregado', 'Dados anteriores foram recuperados');
            return true;
        } catch (e) {
            console.error('Draft load error', e);
            return false;
        }
    };

    const clearDraft = () => {
        localStorage.removeItem(draftKey);
    };

    /* =========================
       Wire core listeners
       ========================= */
    // Add variation
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

    // Clear all (mantém 1)
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

    // Image preview
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
                renderImagePreview(ev.target.result, false);
                scheduleDraftSave(); // salva preview base64
            };
            reader.readAsDataURL(file);
        });
    }

    // desc count
    const updateDescCount = () => {
        if (!desc || !descCount) return;
        descCount.textContent = String((desc.value || '').length);
    };

    if (desc) desc.addEventListener('input', () => { updateDescCount(); scheduleDraftSave(); });
    if (nome) nome.addEventListener('input', () => { updateProgress(); scheduleDraftSave(); });
    if (categoria) categoria.addEventListener('change', () => { updateProgress(); scheduleDraftSave(); });

    // status checks -> save
    ['Ativo', 'EmPromocao', 'MaisVendido', 'RequerMaioridade'].forEach(n => {
        const el = document.querySelector(`[name="${n}"]`);
        if (el) el.addEventListener('change', () => { updateProgress(); scheduleDraftSave(); });
    });

    // type product
    tipoRadios.forEach(r => r.addEventListener('change', applyTypeUI));
    typeCards.forEach(card => {
        card.addEventListener('click', () => {
            const t = card.dataset.type;
            const radio = tipoRadios.find(r => r.value === t);
            if (radio) {
                radio.checked = true;
                applyTypeUI();
            }
        });
    });

    // submit: valida + normaliza + limpa draft se ok
    if (form) {
        form.addEventListener('submit', (e) => {
            let ok = true;

            variationsList.querySelectorAll('.pf2-vari').forEach(card => {
                syncName(card);

                const p = card.querySelector('.v-price');
                const pr = card.querySelector('.v-promo');
                if (p) maskMoneyOnBlur(p);
                if (pr) maskMoneyOnBlur(pr);

                if (!validateVariationCard(card)) ok = false;
            });

            calcTotalStock();
            updateProgress();

            if (!ok) {
                e.preventDefault();
                toast('err', 'Erro no formulário', 'Verifique as variações: Nome e Preço são obrigatórios');
                return;
            }

            // sucesso: limpa rascunho (o server vai redirecionar)
            clearDraft();
            toast('ok', 'Enviando', 'Salvando produto...');
        });
    }

    /* =========================
       Init
       ========================= */
    // tenta carregar draft primeiro
    const loaded = loadDraft();

    // se não carregou draft, só "wire" o que veio do server
    if (!loaded) {
        variationsList.querySelectorAll('.pf2-vari').forEach(wireVariation);
        renumber();
    }

    updateDescCount();
    applyTypeUI();
    calcTotalStock();
    updateProgress();
});
