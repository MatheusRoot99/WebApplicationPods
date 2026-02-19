document.addEventListener('DOMContentLoaded', () => {
    if (window.__pf2ProdutoFormInit) return;
    window.__pf2ProdutoFormInit = true;

    const form = document.getElementById('productForm');
    if (!form) return;

    // ===== Variações (Padrão/Bebida) =====
    const variationsList = document.getElementById('variationsList');
    const btnAddVariation = document.getElementById('addVariation');
    const btnClearAllVariations = document.getElementById('clearAllVariations');
    const totalStockEl = document.getElementById('totalStock');
    const totalStockInput = document.getElementById('totalStockInput');

    // ===== Sabores (Padrão/Bebida) =====
    const saboresSection = document.getElementById('saboresSection');
    const flavorsList = document.getElementById('flavorsList');
    const btnAddFlavor = document.getElementById('addFlavor');
    const btnClearFlavors = document.getElementById('clearAllFlavors');
    const flavorTemplate = document.getElementById('flavorTemplate');

    // ===== POD: infos + lista =====
    const podInfoSection = document.getElementById('podInfoSection');
    const podSection = document.getElementById('podSaboresVariacoesSection');
    const podList = document.getElementById('podList');
    const podTemplate = document.getElementById('podTemplate');
    const btnAddPodRow = document.getElementById('addPodRow');
    const btnClearAllPodRows = document.getElementById('clearAllPodRows');

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

    // ===== Toast =====
    const toastHost = document.getElementById('pf2ToastHost');
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
       ✅ NOVO: desabilita inputs de seções escondidas
       (evita ModelState quebrar por campos duplicados/invisíveis)
       ========================= */
    const setSectionEnabled = (rootEl, enabled) => {
        if (!rootEl) return;

        const nodes = rootEl.querySelectorAll('input, select, textarea, button');
        nodes.forEach(el => {
            // NÃO desabilitar botões principais do form geral
            // aqui só entram botões dentro da própria seção
            if (enabled) {
                el.disabled = false;
            } else {
                el.disabled = true;
            }
        });
    };

    /* =========================
       Money mask (pt-BR)
       ========================= */
    const onlyDigits = (s) => (s || '').replace(/\D/g, '');
    const moneyFromDigits = (digits) => {
        const n = Number(digits || 0) / 100;
        return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    };
    const normalizeMoney = (val) => moneyFromDigits(onlyDigits(val));
    const wireMoney = (input) => {
        if (!input) return;

        input.addEventListener('focus', () => {
            if (!input.value || input.value.trim() === '') input.value = '0,00';
            const len = input.value.length;
            try { input.setSelectionRange(len, len); } catch { }
        });

        input.addEventListener('input', () => {
            const digits = onlyDigits(input.value).slice(0, 12);
            input.value = moneyFromDigits(digits);
            const len = input.value.length;
            try { input.setSelectionRange(len, len); } catch { }
        });

        input.addEventListener('blur', () => {
            input.value = normalizeMoney(input.value);
        });
    };

    /* =========================
       Tipo Produto UI
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
    const isPodType = () => getType() === 'pod';

    const applyTypeUI = () => {
        const t = getType();

        typeCards.forEach(card => {
            card.classList.toggle('is-selected', card.dataset.type === t);
        });

        if (tipoHidden) tipoHidden.value = uiToEnumInt(t);

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

        // ===== SHOW/HIDE + ✅ ENABLE/DISABLE por tipo =====
        const pod = isPodType();

        // POD sections
        if (podInfoSection) podInfoSection.style.display = pod ? '' : 'none';
        if (podSection) podSection.style.display = pod ? '' : 'none';
        setSectionEnabled(podInfoSection, pod);
        setSectionEnabled(podSection, pod);

        // Padrão/Bebida sections
        if (saboresSection) saboresSection.style.display = pod ? 'none' : '';
        // O card de variações padrão é o ".pf2-card" que envolve variationsList
        const variacoesCard = (variationsList && variationsList.closest('.pf2-card')) ? variationsList.closest('.pf2-card') : null;
        if (variacoesCard) variacoesCard.style.display = pod ? 'none' : '';

        setSectionEnabled(saboresSection, !pod);
        setSectionEnabled(variacoesCard, !pod);

        // recalcula
        calcTotalStock();
        updateProgress();
    };

    const forceTypeFromHidden = () => {
        if (!tipoHidden) return;
        const ui = enumIntToUi(tipoHidden.value);
        const radio = tipoRadios.find(r => r.value === ui);
        if (radio) radio.checked = true;
        applyTypeUI();
    };

    /* =========================
       PADRÃO/BEBIDA - Sabores (texto)
       ========================= */
    const syncFlavorBadge = (row) => {
        const badge = row.querySelector('.fl-badge');
        const real = row.querySelector('.fl-name');
        if (!badge) return;
        const val = (real?.value || '').trim();
        badge.textContent = val ? val : '—';
    };

    const renumberFlavors = () => {
        if (!flavorsList) return;
        const rows = Array.from(flavorsList.querySelectorAll('.flavor-row'));
        rows.forEach((row, idx) => {
            row.dataset.index = idx;
            row.querySelectorAll('[name]').forEach(el => {
                const n = el.getAttribute('name');
                if (!n) return;
                el.setAttribute('name', n.replace(/Sabores\[\d+\]/g, `Sabores[${idx}]`));
            });
        });
    };

    const createFlavorRow = (idx) => {
        if (!flavorTemplate) return null;
        const html = flavorTemplate.innerHTML.replaceAll('__i__', String(idx));
        const wrap = document.createElement('div');
        wrap.innerHTML = html.trim();
        return wrap.firstElementChild;
    };

    if (flavorsList) {
        flavorsList.addEventListener('click', (e) => {
            const btn = e.target.closest('.fl-remove');
            if (!btn) return;

            const row = btn.closest('.flavor-row');
            if (!row) return;

            const count = flavorsList.querySelectorAll('.flavor-row').length;
            if (count <= 1) {
                row.querySelector('.fl-name') && (row.querySelector('.fl-name').value = '');
                syncFlavorBadge(row);
                toast('warn', 'Atenção', 'Mantemos pelo menos 1 linha de sabor.');
                return;
            }

            row.remove();
            renumberFlavors();
            toast('info', 'Sabor removido');
        });

        flavorsList.addEventListener('input', (e) => {
            const row = e.target.closest('.flavor-row');
            if (!row) return;
            syncFlavorBadge(row);
        });
    }

    if (btnAddFlavor && flavorsList) {
        btnAddFlavor.addEventListener('click', () => {
            const idx = flavorsList.querySelectorAll('.flavor-row').length;
            const row = createFlavorRow(idx);
            if (!row) return;

            flavorsList.appendChild(row);
            renumberFlavors();
            toast('ok', 'Sabor adicionado');
        });
    }

    if (btnClearFlavors && flavorsList) {
        btnClearFlavors.addEventListener('click', () => {
            if (!confirm('Tem certeza que deseja limpar os sabores?')) return;

            flavorsList.innerHTML = '';
            const row = createFlavorRow(0);
            if (row) flavorsList.appendChild(row);

            renumberFlavors();
            toast('warn', 'Sabores limpos', 'Mantivemos 1 linha');
        });
    }

    /* =========================
       POD - Sabores e Variações (cada linha = 1 sabor)
       ========================= */
    const syncPodBadge = (row) => {
        const badge = row.querySelector('.pod-badge');
        const real = row.querySelector('.pod-name');
        if (!badge) return;
        const val = (real?.value || '').trim();
        badge.textContent = val ? val : '—';
    };

    const applyPodSelectToName = (row) => {
        const sel = row.querySelector('.pod-select');
        const name = row.querySelector('.pod-name');
        const other = row.querySelector('.pod-other');
        if (!sel || !name) return;

        const v = sel.value || '';

        if (v === '__OUTRO__') {
            if (other) other.classList.remove('hidden');
            if (other && other.value && other.value.trim()) name.value = other.value.trim();
        } else if (v) {
            if (other) { other.classList.add('hidden'); other.value = ''; }
            name.value = v;
        } else {
            if (other) { other.classList.add('hidden'); other.value = ''; }
        }

        syncPodBadge(row);
    };

    const renumberPod = () => {
        if (!podList) return;
        const rows = Array.from(podList.querySelectorAll('.pod-row'));
        rows.forEach((row, idx) => {
            row.dataset.index = idx;
            row.querySelectorAll('[name]').forEach(el => {
                const n = el.getAttribute('name');
                if (!n) return;
                el.setAttribute('name', n.replace(/Variacoes\[\d+\]/g, `Variacoes[${idx}]`));
            });
        });
    };

    const createPodRow = (idx) => {
        if (!podTemplate) return null;
        const html = podTemplate.innerHTML.replaceAll('__i__', String(idx));
        const wrap = document.createElement('div');
        wrap.innerHTML = html.trim();
        return wrap.firstElementChild;
    };

    if (podList) {
        podList.querySelectorAll('.pod-price').forEach(wireMoney);
        podList.querySelectorAll('.pod-promo').forEach(wireMoney);

        podList.addEventListener('click', (e) => {
            const btn = e.target.closest('.pod-remove');
            if (!btn) return;

            const row = btn.closest('.pod-row');
            if (!row) return;

            const count = podList.querySelectorAll('.pod-row').length;
            if (count <= 1) {
                row.querySelector('.pod-name') && (row.querySelector('.pod-name').value = '');
                row.querySelector('.pod-stock') && (row.querySelector('.pod-stock').value = '0');
                row.querySelector('.pod-price') && (row.querySelector('.pod-price').value = '');
                row.querySelector('.pod-promo') && (row.querySelector('.pod-promo').value = '');
                row.querySelector('.pod-select') && (row.querySelector('.pod-select').value = '');
                const other = row.querySelector('.pod-other');
                if (other) { other.value = ''; other.classList.add('hidden'); }
                syncPodBadge(row);
                toast('warn', 'Atenção', 'Mantemos pelo menos 1 linha.');
                calcTotalStock();
                return;
            }

            row.remove();
            renumberPod();
            toast('info', 'Linha removida');
            calcTotalStock();
            updateProgress();
        });

        podList.addEventListener('change', (e) => {
            const row = e.target.closest('.pod-row');
            if (!row) return;

            if (e.target.classList.contains('pod-select')) {
                applyPodSelectToName(row);
            }

            if (e.target.classList.contains('pod-active')) {
                calcTotalStock();
                updateProgress();
            }
        });

        podList.addEventListener('input', (e) => {
            const row = e.target.closest('.pod-row');
            if (!row) return;

            if (e.target.classList.contains('pod-other')) {
                const sel = row.querySelector('.pod-select');
                const name = row.querySelector('.pod-name');
                if (sel && sel.value === '__OUTRO__' && name) name.value = e.target.value.trim();
            }

            if (e.target.classList.contains('pod-name')) syncPodBadge(row);

            if (e.target.classList.contains('pod-stock')) {
                calcTotalStock();
                updateProgress();
            }
        });
    }

    if (btnAddPodRow && podList) {
        btnAddPodRow.addEventListener('click', () => {
            const idx = podList.querySelectorAll('.pod-row').length;
            const row = createPodRow(idx);
            if (!row) return;

            row.querySelectorAll('.pod-price').forEach(wireMoney);
            row.querySelectorAll('.pod-promo').forEach(wireMoney);

            podList.appendChild(row);
            renumberPod();
            toast('ok', 'Sabor adicionado');
        });
    }

    if (btnClearAllPodRows && podList) {
        btnClearAllPodRows.addEventListener('click', () => {
            if (!confirm('Tem certeza que deseja limpar?')) return;

            podList.innerHTML = '';
            const row = createPodRow(0);
            if (row) {
                row.querySelectorAll('.pod-price').forEach(wireMoney);
                row.querySelectorAll('.pod-promo').forEach(wireMoney);
                podList.appendChild(row);
            }

            renumberPod();
            toast('warn', 'Limpo', 'Mantivemos 1 linha');
            calcTotalStock();
            updateProgress();
        });
    }

    /* =========================
       Variações PADRÃO/BEBIDA
       ========================= */
    const syncVariationName = (card) => {
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

    const renumberVariations = () => {
        if (!variationsList) return;
        const cards = Array.from(variationsList.querySelectorAll('.pf2-vari'));
        cards.forEach((card, idx) => {
            card.dataset.index = idx;
            card.querySelectorAll('[name]').forEach(el => {
                const n = el.getAttribute('name');
                if (!n) return;
                el.setAttribute('name', n.replace(/Variacoes\[\d+\]/g, `Variacoes[${idx}]`));
            });
        });
    };

    const validateVariationCard = (card) => {
        syncVariationName(card);

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

    /* =========================
       Estoque total
       ========================= */
    const calcTotalStock = () => {
        let total = 0;

        if (isPodType()) {
            if (podList) {
                podList.querySelectorAll('.pod-row').forEach(row => {
                    const stock = parseInt(row.querySelector('.pod-stock')?.value, 10) || 0;
                    const active = row.querySelector('.pod-active')?.checked ?? true;
                    if (active) total += stock;
                });
            }
        } else {
            if (variationsList) {
                variationsList.querySelectorAll('.pf2-vari').forEach(card => {
                    const stock = parseInt(card.querySelector('.v-stock')?.value, 10) || 0;
                    const mult = parseInt(card.querySelector('.v-mult')?.value, 10) || 1;
                    const active = card.querySelector('.v-active')?.checked ?? true;
                    if (active) total += stock * mult;
                });
            }
        }

        const txt = total.toLocaleString('pt-BR');
        if (totalStockEl) totalStockEl.textContent = txt;
        if (totalStockInput) totalStockInput.value = txt;
        return total;
    };

    const updateProgress = () => {
        if (!progressText || !progressBar) return;

        let done = 0;
        const total = 3;

        const nomeOkNow = (nome?.value || '').trim().length > 0;
        if (nomeOkNow) done++;

        const catOkNow = (categoria?.value || '').toString().trim().length > 0 && categoria.value !== "0";
        if (catOkNow) done++;

        let hasValidVar = true;

        if (isPodType()) {
            hasValidVar = podList
                ? Array.from(podList.querySelectorAll('.pod-row')).some(r => {
                    const sabor = (r.querySelector('.pod-name')?.value || '').trim();
                    const price = (r.querySelector('.pod-price')?.value || '').trim();
                    return !!sabor && !!price && normalizeMoney(price) !== '0,00';
                })
                : true;
        } else {
            hasValidVar = variationsList
                ? Array.from(variationsList.querySelectorAll('.pf2-vari')).some(validateVariationCard)
                : true;
        }

        if (hasValidVar) done++;

        const pct = Math.round((done / total) * 100);
        progressText.textContent = `${pct}%`;
        progressBar.style.width = `${pct}%`;

        if (nomeOk) nomeOk.style.display = nomeOkNow ? 'block' : 'none';
    };

    // ===== Eventos das variações (padrão/bebida)
    if (variationsList) {
        variationsList.querySelectorAll('.v-price').forEach(wireMoney);
        variationsList.querySelectorAll('.v-promo').forEach(wireMoney);

        variationsList.addEventListener('click', (e) => {
            const btn = e.target.closest('.v-remove');
            if (!btn) return;

            const card = btn.closest('.pf2-vari');
            if (!card) return;

            const count = variationsList.querySelectorAll('.pf2-vari').length;
            if (count <= 1) {
                toast('err', 'Erro', 'O produto precisa ter pelo menos uma variação.');
                return;
            }

            card.remove();
            renumberVariations();
            calcTotalStock();
            updateProgress();
            toast('info', 'Variação removida');
        });

        variationsList.addEventListener('input', (e) => {
            const card = e.target.closest('.pf2-vari');
            if (!card) return;

            if (e.target.classList.contains('v-custom') ||
                e.target.classList.contains('v-mult') ||
                e.target.classList.contains('v-stock')) {
                validateVariationCard(card);
                calcTotalStock();
                updateProgress();
            }
        });

        variationsList.addEventListener('change', (e) => {
            const card = e.target.closest('.pf2-vari');
            if (!card) return;

            if (e.target.classList.contains('v-name')) {
                syncVariationName(card);
                validateVariationCard(card);
                calcTotalStock();
                updateProgress();
            }

            if (e.target.classList.contains('v-active')) {
                calcTotalStock();
                updateProgress();
            }
        });
    }

    if (btnAddVariation && variationsList) {
        btnAddVariation.addEventListener('click', () => {
            const idx = variationsList.querySelectorAll('.pf2-vari').length;

            const base = variationsList.querySelector('.pf2-vari');
            if (!base) return;

            const clone = base.cloneNode(true);
            clone.dataset.index = idx;

            clone.querySelectorAll('input[type="hidden"][name$=".Id"]').forEach(h => h.value = '');
            const hiddenName = clone.querySelector('.v-name-hidden');
            if (hiddenName) hiddenName.value = 'Unidade';

            const badge = clone.querySelector('.v-badge');
            if (badge) badge.textContent = 'Unidade';

            const sel = clone.querySelector('.v-name');
            if (sel) sel.value = 'Unidade';

            const custom = clone.querySelector('.v-custom');
            if (custom) { custom.value = ''; custom.classList.add('hidden'); }

            const mult = clone.querySelector('.v-mult');
            if (mult) mult.value = '1';

            const stock = clone.querySelector('.v-stock');
            if (stock) stock.value = '0';

            const price = clone.querySelector('.v-price');
            if (price) price.value = '';

            const promo = clone.querySelector('.v-promo');
            if (promo) promo.value = '';

            const active = clone.querySelector('.v-active');
            if (active) active.checked = true;

            clone.querySelectorAll('.v-price').forEach(wireMoney);
            clone.querySelectorAll('.v-promo').forEach(wireMoney);

            variationsList.appendChild(clone);
            renumberVariations();
            calcTotalStock();
            updateProgress();
            toast('ok', 'Variação adicionada');
        });
    }

    if (btnClearAllVariations && variationsList) {
        btnClearAllVariations.addEventListener('click', () => {
            if (!confirm('Tem certeza que deseja excluir TODAS as variações?')) return;

            const base = variationsList.querySelector('.pf2-vari');
            if (!base) return;

            variationsList.innerHTML = '';
            const first = base.cloneNode(true);

            first.dataset.index = 0;
            first.querySelectorAll('input[type="hidden"][name$=".Id"]').forEach(h => h.value = '');

            const hiddenName = first.querySelector('.v-name-hidden');
            if (hiddenName) hiddenName.value = 'Unidade';

            const badge = first.querySelector('.v-badge');
            if (badge) badge.textContent = 'Unidade';

            const sel = first.querySelector('.v-name');
            if (sel) sel.value = 'Unidade';

            const custom = first.querySelector('.v-custom');
            if (custom) { custom.value = ''; custom.classList.add('hidden'); }

            const mult = first.querySelector('.v-mult');
            if (mult) mult.value = '1';

            const stock = first.querySelector('.v-stock');
            if (stock) stock.value = '0';

            const price = first.querySelector('.v-price');
            if (price) price.value = '';

            const promo = first.querySelector('.v-promo');
            if (promo) promo.value = '';

            const active = first.querySelector('.v-active');
            if (active) active.checked = true;

            first.querySelectorAll('.v-price').forEach(wireMoney);
            first.querySelectorAll('.v-promo').forEach(wireMoney);

            variationsList.appendChild(first);
            renumberVariations();
            calcTotalStock();
            updateProgress();
            toast('warn', 'Variações limpas', 'Mantivemos 1 variação (obrigatório)');
        });
    }

    // ===== Tipo (somente se não estiver travado)
    const typeIsLocked = tipoRadios.some(r => r.disabled);
    if (!typeIsLocked) {
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
    }

    // ===== Desc counter + progress
    const updateDescCount = () => {
        if (!desc || !descCount) return;
        descCount.textContent = String((desc.value || '').length);
    };
    if (desc) desc.addEventListener('input', () => { updateDescCount(); });
    if (nome) nome.addEventListener('input', () => { updateProgress(); });
    if (categoria) categoria.addEventListener('change', () => { updateProgress(); });

    // ===== Submit: normaliza money
    form.addEventListener('submit', (e) => {
        let ok = true;

        if (isPodType()) {
            if (podList) {
                podList.querySelectorAll('.pod-row').forEach(row => {
                    const p = row.querySelector('.pod-price');
                    const pr = row.querySelector('.pod-promo');
                    if (p) p.value = normalizeMoney(p.value);
                    if (pr) pr.value = normalizeMoney(pr.value);

                    const sabor = (row.querySelector('.pod-name')?.value || '').trim();
                    if (!sabor) ok = false;

                    const price = (p?.value || '').trim();
                    if (!price || price === '0,00') ok = false;
                });
            }
        } else {
            if (variationsList) {
                variationsList.querySelectorAll('.pf2-vari').forEach(card => {
                    syncVariationName(card);
                    const p = card.querySelector('.v-price');
                    const pr = card.querySelector('.v-promo');
                    if (p) p.value = normalizeMoney(p.value);
                    if (pr) pr.value = normalizeMoney(pr.value);

                    if (!validateVariationCard(card)) ok = false;
                });
            }
        }

        calcTotalStock();
        updateProgress();

        if (!ok) {
            e.preventDefault();
            toast('err', 'Erro no formulário', 'Verifique os campos obrigatórios.');
        }
    });

    // INIT
    updateDescCount();
    forceTypeFromHidden();
    calcTotalStock();
    updateProgress();
});
