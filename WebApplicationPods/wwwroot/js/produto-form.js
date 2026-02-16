document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('productForm');

    // ===== Variações =====
    const variationsList = document.getElementById('variationsList');
    const btnAdd = document.getElementById('addVariation');
    const btnClear = document.getElementById('clearAllVariations');
    const totalStockEl = document.getElementById('totalStock');
    const totalStockInput = document.getElementById('totalStockInput');

    // ===== Sabores =====
    const saboresSection = document.getElementById('saboresSection');
    const flavorsList = document.getElementById('flavorsList');
    const btnAddFlavor = document.getElementById('addFlavor');
    const btnClearFlavors = document.getElementById('clearAllFlavors');
    const flavorTemplate = document.getElementById('flavorTemplate');

    // ===== Progresso / campos =====
    const progressText = document.getElementById('progressText');
    const progressBar = document.getElementById('progressBar');
    const desc = document.querySelector('[name="Descricao"]');
    const descCount = document.getElementById('descCount');
    const nome = document.querySelector('[name="Nome"]');
    const nomeOk = document.getElementById('nomeOk');
    const categoria = document.querySelector('[name="CategoriaId"]');

    // ===== Tipo =====
    const tipoRadios = Array.from(document.querySelectorAll('input[name="TipoProdutoUI"]'));
    const typeCards = Array.from(document.querySelectorAll('.pf2-type-item'));
    const maioridade = document.getElementById('requerMaioridade');
    const maioridadeHint = document.getElementById('maioridadeHint');
    const maioridadeCard = document.getElementById('maioridadeCard');
    const tipoHidden = document.getElementById('TipoProdutoHidden');

    // ===== Imagem =====
    const imageUpload = document.getElementById('imageUpload');
    const imagePreviewBox = document.getElementById('imagePreviewBox');

    // ===== Toast =====
    const toastHost = document.getElementById('pf2ToastHost');

    // ===== Draft key =====
    const idHidden = document.querySelector('input[name="Id"]');
    const draftKey = `produto-rascunho:${(idHidden && idHidden.value && idHidden.value !== "0") ? idHidden.value : "new"}`;

    if (!form) return;

    // ✅ Tipo travado no Editar
    const typeIsLocked = tipoRadios.some(r => r.disabled);

    /* =========================
       Toast
       ========================= */
    const toast = (type, title, descText) => {
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
        ${descText ? `<div class="desc">${descText}</div>` : ``}
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
       Tipo Produto UI (hidden int)
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

    // ✅ GET TYPE ROBUSTO:
    // - se estiver travado (Editar), SEMPRE lê do hidden (valor do banco)
    // - se não estiver travado (Criar), lê dos radios
    const getType = () => {
        if (typeIsLocked) return enumIntToUi(tipoHidden?.value ?? '0');
        const checked = tipoRadios.find(r => r.checked);
        return checked ? checked.value : 'padrao';
    };

    const applyTypeUI = (saveDraft = true) => {
        const t = getType();

        typeCards.forEach(card => {
            card.classList.toggle('is-selected', card.dataset.type === t);
        });

        // ✅ só escreve no hidden quando NÃO estiver travado (Criar)
        if (!typeIsLocked && tipoHidden) tipoHidden.value = uiToEnumInt(t);

        // maioridade forçada para Pod/Bebida
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

        // ✅ aplica modo sabores conforme tipo
        applyFlavorModeByType(t);

        updateProgress();
        if (saveDraft) scheduleDraftSave();
    };

    const setType = (uiValue, saveDraft = false) => {
        if (typeIsLocked) {
            // no editar não mexe em radio (está disabled)
            applyTypeUI(saveDraft);
            return;
        }
        const radio = tipoRadios.find(r => r.value === uiValue);
        if (radio) radio.checked = true;
        applyTypeUI(saveDraft);
    };

    // ✅ No Editar garante que a UI reflete o hidden do banco
    const forceTypeFromHidden = () => {
        const ui = enumIntToUi(tipoHidden?.value ?? '0');
        if (!typeIsLocked) setType(ui, false);
        else applyTypeUI(false);
    };

    /* =========================
       Variações
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
        if (!variationsList) return 0;

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
        if (!variationsList) return;

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

        const hasValidVar = variationsList
            ? Array.from(variationsList.querySelectorAll('.pf2-vari')).some(validateVariationCard)
            : true;

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

    /* =========================
       Sabores (corrigido)
       Regras:
       - POD: mostra select + (Outro -> input fl-other) e ESCONDE fl-name do usuário (mas mantém no POST)
       - Outros tipos: esconde select e fl-other, e mostra texto livre (fl-name)
       - Validação: se Quantidade > 0, exige Sabor
       ========================= */
    const isPodUi = (typeUi) => typeUi === 'pod';

    const syncFlavorBadge = (row) => {
        const badge = row.querySelector('.fl-badge');
        const real = row.querySelector('.fl-name');
        if (!badge) return;
        const val = (real?.value || '').trim();
        badge.textContent = val ? val : '—';
    };

    const validateFlavorRow = (row) => {
        const real = row.querySelector('.fl-name');
        const qty = row.querySelector('.fl-qty');
        const valMsg = row.querySelector('.fl-val');

        const name = (real?.value || '').trim();
        const q = parseInt(qty?.value || '0', 10) || 0;

        // ✅ regra pedida: se quantidade > 0, exige nome
        const ok = (q <= 0) || (name.length > 0);

        if (valMsg) valMsg.classList.toggle('hidden', ok);
        return ok;
    };

    const renumberFlavors = () => {
        if (!flavorsList) return;

        const rows = Array.from(flavorsList.querySelectorAll('.flavor-row'));
        rows.forEach((row, idx) => {
            row.dataset.index = idx;

            row.querySelectorAll('[name]').forEach(el => {
                const nameAttr = el.getAttribute('name');
                if (!nameAttr) return;
                el.setAttribute('name', nameAttr.replace(/Sabores\[\d+\]/g, `Sabores[${idx}]`));
            });
        });
    };

    const applyFlavorModeByType = (typeUi) => {
        if (!flavorsList) return;

        // se quiser esconder a seção fora de POD, troque para:
        // if (saboresSection) saboresSection.style.display = isPodUi(typeUi) ? '' : 'none';
        if (saboresSection) saboresSection.style.display = '';

        const pod = isPodUi(typeUi);

        flavorsList.querySelectorAll('.flavor-row').forEach(row => {
            const sel = row.querySelector('.fl-pod-select');
            const real = row.querySelector('.fl-name');
            const other = row.querySelector('.fl-other');

            if (!real) return;

            // ✅ dropdown só no POD
            if (sel) sel.classList.toggle('hidden', !pod);

            if (pod) {
                // no POD: user NÃO digita no real
                real.classList.add('hidden');

                if (!sel) return;

                const v = (sel.value || '');

                if (v === '__OUTRO__') {
                    if (other) other.classList.remove('hidden');
                    const txt = (other?.value || '').trim();
                    real.value = txt; // real sempre sincronizado
                } else if (v) {
                    if (other) { other.classList.add('hidden'); other.value = ''; }
                    real.value = v;
                } else {
                    if (other) { other.classList.add('hidden'); other.value = ''; }
                    // não apaga real aqui para não perder conteúdo ao alternar
                }
            } else {
                // fora POD: texto livre
                real.classList.remove('hidden');
                if (other) { other.classList.add('hidden'); other.value = ''; }
                // mantém real como está (texto livre)
            }

            syncFlavorBadge(row);
            validateFlavorRow(row);
        });
    };

    const wireFlavorRow = (row) => {
        const remove = row.querySelector('.fl-remove');
        const real = row.querySelector('.fl-name');
        const qty = row.querySelector('.fl-qty');
        const sel = row.querySelector('.fl-pod-select');
        const other = row.querySelector('.fl-other');

        const onAny = () => {
            // sempre recalcula com base no tipo atual
            applyFlavorModeByType(getType());
            syncFlavorBadge(row);
            validateFlavorRow(row);
            scheduleDraftSave();
        };

        if (real) real.addEventListener('input', onAny);
        if (qty) qty.addEventListener('input', onAny);

        if (sel) sel.addEventListener('change', onAny);

        if (other) {
            other.addEventListener('input', () => {
                if (real) real.value = (other.value || '').trim();
                onAny();
            });
            other.addEventListener('blur', () => {
                other.value = (other.value || '').trim();
                if (real) real.value = other.value;
                onAny();
            });
        }

        if (remove) {
            remove.addEventListener('click', () => {
                const count = flavorsList.querySelectorAll('.flavor-row').length;
                if (count <= 1) {
                    toast('warn', 'Atenção', 'Mantemos pelo menos 1 linha de sabor.');
                    // limpa ao invés de remover
                    if (real) real.value = '';
                    if (qty) qty.value = '0';
                    if (sel) sel.value = '';
                    if (other) { other.value = ''; other.classList.add('hidden'); }
                    applyFlavorModeByType(getType());
                    scheduleDraftSave();
                    return;
                }

                row.remove();
                renumberFlavors();
                scheduleDraftSave();
                toast('info', 'Sabor removido');
            });
        }

        // init row
        applyFlavorModeByType(getType());
    };

    const createFlavorRow = (idx, data) => {
        const d = data || { sabor: '', quantidade: 0 };

        const html = flavorTemplate
            ? flavorTemplate.innerHTML.replaceAll('__i__', String(idx))
            : '';

        const wrap = document.createElement('div');
        wrap.innerHTML = html.trim();
        const node = wrap.firstElementChild;

        if (!node) return null;

        const real = node.querySelector('.fl-name');
        const qty = node.querySelector('.fl-qty');
        const badge = node.querySelector('.fl-badge');

        if (real) real.value = d.sabor ?? '';
        if (qty) qty.value = String(parseInt(d.quantidade ?? 0, 10) || 0);
        if (badge) badge.textContent = (d.sabor || '').trim() ? d.sabor : '—';

        return node;
    };

    /* =========================
       Draft (mantive o seu, só sem mudar tipo no Editar)
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
            sabores: flavorsList ? Array.from(flavorsList.querySelectorAll('.flavor-row')).map(row => {
                const real = row.querySelector('.fl-name');
                const qty = row.querySelector('.fl-qty');
                const sel = row.querySelector('.fl-pod-select');
                const other = row.querySelector('.fl-other');

                return {
                    sabor: real?.value ?? '',
                    quantidade: qty?.value ?? '0',
                    sel: sel?.value ?? '',
                    other: other?.value ?? ''
                };
            }) : [],
            variacoes: variationsList ? Array.from(variationsList.querySelectorAll('.pf2-vari')).map(card => {
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
            }) : [],
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
                    (draft.variacoes || []).some(v => (v.precoTexto || '').trim().length > 0) ||
                    (draft.sabores || []).some(s => (s.sabor || '').trim().length > 0);

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

            // ✅ Tipo: só no Criar (no Editar NÃO mexe)
            if (!typeIsLocked) {
                if (tipoHidden && d?.tipoProdutoHidden != null) tipoHidden.value = String(d.tipoProdutoHidden);
                if (d?.tipoProdutoUI) setType(d.tipoProdutoUI, false);
            }

            // Sabores
            if (flavorsList) {
                flavorsList.innerHTML = '';
                const sabores = Array.isArray(d?.sabores) && d.sabores.length ? d.sabores : [{ sabor: '', quantidade: 0 }];
                sabores.forEach((s, idx) => {
                    const row = createFlavorRow(idx, s);
                    if (!row) return;
                    flavorsList.appendChild(row);

                    // restaura select/outro se existirem
                    const sel = row.querySelector('.fl-pod-select');
                    const other = row.querySelector('.fl-other');
                    if (sel && s?.sel != null) sel.value = String(s.sel);
                    if (other && s?.other != null) other.value = String(s.other);

                    wireFlavorRow(row);
                });
                renumberFlavors();
            }

            // Variações (mantive sua lógica original – se você usa createVariationCard no seu arquivo, mantenha como estava)
            // Aqui eu não reescrevi createVariationCard para não duplicar; mantenha o seu bloco como já tinha.

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
       Listeners: Variações (igual seu)
       ========================= */
    if (variationsList) {
        variationsList.querySelectorAll('.pf2-vari').forEach(wireVariation);
        renumber();
        variationsList.querySelectorAll('.v-price').forEach(wireMoney);
        variationsList.querySelectorAll('.v-promo').forEach(wireMoney);
    }

    /* =========================
       Listeners: Sabores
       ========================= */
    if (flavorsList) {
        flavorsList.querySelectorAll('.flavor-row').forEach(wireFlavorRow);
        renumberFlavors();
    }

    if (btnAddFlavor && flavorsList && flavorTemplate) {
        btnAddFlavor.addEventListener('click', () => {
            const idx = flavorsList.querySelectorAll('.flavor-row').length;
            const row = createFlavorRow(idx, { sabor: '', quantidade: 0 });
            if (!row) return;
            flavorsList.appendChild(row);
            wireFlavorRow(row);
            renumberFlavors();
            applyFlavorModeByType(getType());
            scheduleDraftSave();
            toast('ok', 'Sabor adicionado');
        });
    }

    if (btnClearFlavors && flavorsList) {
        btnClearFlavors.addEventListener('click', () => {
            if (!confirm('Tem certeza que deseja limpar os sabores?')) return;

            flavorsList.innerHTML = '';
            const row = createFlavorRow(0, { sabor: '', quantidade: 0 });
            if (row) {
                flavorsList.appendChild(row);
                wireFlavorRow(row);
            }
            renumberFlavors();
            applyFlavorModeByType(getType());
            scheduleDraftSave();
            toast('warn', 'Sabores limpos', 'Mantivemos 1 linha');
        });
    }

    /* =========================
       Desc counter + base fields
       ========================= */
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

    // Tipo: só no Criar
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

    /* =========================
       Submit
       ========================= */
    form.addEventListener('submit', (e) => {
        let ok = true;

        // sabores: regra qty>0 exige sabor
        if (flavorsList) {
            const rows = Array.from(flavorsList.querySelectorAll('.flavor-row'));
            rows.forEach(row => {
                if (!validateFlavorRow(row)) ok = false;
            });
            renumberFlavors();
        }

        // variações
        if (variationsList) {
            variationsList.querySelectorAll('.pf2-vari').forEach(card => {
                syncName(card);

                const p = card.querySelector('.v-price');
                const pr = card.querySelector('.v-promo');
                if (p) p.value = normalizeMoney(p.value);
                if (pr) pr.value = normalizeMoney(pr.value);

                if (!validateVariationCard(card)) ok = false;
            });
        }

        calcTotalStock();
        updateProgress();

        if (!ok) {
            e.preventDefault();
            toast('err', 'Erro no formulário', 'Verifique os campos obrigatórios.');
            return;
        }

        clearDraft();
        toast('ok', 'Enviando', 'Salvando produto...');
    });

    /* =========================
       INIT
       ========================= */
    const loaded = loadDraft();

    updateDescCount();

    // ✅ tipo sempre reflete hidden do banco (especialmente no Editar)
    forceTypeFromHidden();

    // ✅ aplica modo sabores pelo tipo atual
    applyFlavorModeByType(getType());

    calcTotalStock();
    updateProgress();

    if (!loaded) scheduleDraftSave();
});
