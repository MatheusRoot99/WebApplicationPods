document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('productForm');
    const variationsTable = document.getElementById('variationsTable');
    const noVariations = document.getElementById('noVariations');

    const totalStockElement = document.getElementById('totalStock');
    const totalStockInput = document.getElementById('totalStockInput');

    const imagePreview = document.getElementById('imagePreview');
    const imageUpload = document.getElementById('imageUpload');
    const imageContainer = document.getElementById('imageContainer');

    const btnAdd = document.getElementById('addVariation');
    const btnClearAll = document.getElementById('clearAllVariations');

    if (!variationsTable || !noVariations || !btnAdd) return;

    function checkEmptyTable() {
        const rows = variationsTable.querySelectorAll('tr.variation-row');
        if (rows.length === 0) {
            noVariations.classList.remove('hidden');
            variationsTable.classList.add('hidden');
        } else {
            noVariations.classList.add('hidden');
            variationsTable.classList.remove('hidden');
        }
    }

    function calculateTotalStock() {
        let total = 0;
        const rows = variationsTable.querySelectorAll('tr.variation-row');

        rows.forEach(row => {
            const stock = parseInt(row.querySelector('.variation-stock')?.value) || 0;
            const multiplier = parseInt(row.querySelector('.variation-multiplier')?.value) || 1;
            const isActive = row.querySelector('.variation-active')?.checked ?? true;

            if (isActive) total += stock * multiplier;
        });

        const totalText = total.toLocaleString('pt-BR');
        if (totalStockElement) totalStockElement.textContent = totalText;
        if (totalStockInput) totalStockInput.value = totalText;

        return total;
    }

    function renumberRows() {
        const rows = Array.from(variationsTable.querySelectorAll('tr.variation-row'));
        rows.forEach((row, idx) => {
            row.dataset.index = idx;

            row.querySelectorAll('[name]').forEach(el => {
                const name = el.getAttribute('name');
                if (!name) return;
                el.setAttribute('name', name.replace(/Variacoes\[\d+\]/g, `Variacoes[${idx}]`));
            });
        });
    }

    function syncNomeRow(row) {
        const sel = row.querySelector('.variation-name');
        const custom = row.querySelector('.variation-custom-name');
        const hidden = row.querySelector('.variation-name-hidden');
        if (!sel || !custom || !hidden) return;

        if (sel.value === 'Outro') {
            custom.classList.remove('hidden');
            hidden.value = (custom.value || '').trim();
        } else {
            custom.classList.add('hidden');
            custom.value = '';
            hidden.value = sel.value;
        }
    }

    function wireRow(row) {
        const nameSelect = row.querySelector('.variation-name');
        const customName = row.querySelector('.variation-custom-name');
        const stockInput = row.querySelector('.variation-stock');
        const multiplierInput = row.querySelector('.variation-multiplier');
        const activeCheckbox = row.querySelector('.variation-active');
        const removeBtn = row.querySelector('.remove-variation');

        if (nameSelect) nameSelect.addEventListener('change', () => syncNomeRow(row));
        if (customName) customName.addEventListener('input', () => syncNomeRow(row));

        const updateStock = () => calculateTotalStock();
        if (stockInput) stockInput.addEventListener('input', updateStock);
        if (multiplierInput) multiplierInput.addEventListener('input', updateStock);
        if (activeCheckbox) activeCheckbox.addEventListener('change', updateStock);

        if (removeBtn) {
            removeBtn.addEventListener('click', function () {
                row.remove();
                renumberRows();
                calculateTotalStock();
                checkEmptyTable();
            });
        }

        syncNomeRow(row);
    }

    function createVariationRow(idx) {
        const row = document.createElement('tr');
        row.className = 'variation-row bg-gray-900/50';
        row.dataset.index = idx;

        row.innerHTML = `
            <td class="py-3 px-4">
                <input type="hidden" name="Variacoes[${idx}].Id" value="" />
                <input type="hidden" class="variation-name-hidden" name="Variacoes[${idx}].Nome" value="Unidade" />

                <div class="flex gap-2">
                    <select class="variation-name w-32 px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-white">
                        <option value="Unidade">Unidade</option>
                        <option value="Caixa">Caixa</option>
                        <option value="Fardo">Fardo</option>
                        <option value="Outro">Outro</option>
                    </select>

                    <input type="text"
                        class="variation-custom-name hidden w-32 px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-white"
                        placeholder="Nome...">
                </div>
            </td>

            <td class="py-3 px-4">
                <input type="number"
                    min="1"
                    value="1"
                    class="variation-multiplier w-20 px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-white"
                    name="Variacoes[${idx}].Multiplicador">
            </td>

            <td class="py-3 px-4">
                <div class="relative">
                    <span class="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400">R$</span>
                    <input type="text"
                        value="0,00"
                        class="variation-price w-28 pl-8 pr-3 py-2 bg-gray-800 border border-gray-700 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-white"
                        placeholder="0,00"
                        name="Variacoes[${idx}].PrecoTexto">
                </div>
            </td>

            <td class="py-3 px-4">
                <div class="relative">
                    <span class="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400">R$</span>
                    <input type="text"
                        value="0,00"
                        class="variation-promo w-28 pl-8 pr-3 py-2 bg-gray-800 border border-gray-700 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-white"
                        placeholder="0,00"
                        name="Variacoes[${idx}].PrecoPromocionalTexto">
                </div>
            </td>

            <td class="py-3 px-4">
                <input type="number"
                    min="0"
                    value="0"
                    class="variation-stock w-20 px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-white"
                    name="Variacoes[${idx}].Estoque">
            </td>

            <td class="py-3 px-4">
                <label class="flex items-center justify-center cursor-pointer">
                    <div class="switch">
                        <input type="checkbox" class="variation-active" checked name="Variacoes[${idx}].Ativo" value="true">
                        <span class="slider"></span>
                    </div>
                </label>
                <input type="hidden" name="Variacoes[${idx}].Ativo" value="false">
            </td>

            <td class="py-3 px-4">
                <button type="button"
                    class="remove-variation remove-btn w-9 h-9 flex items-center justify-center rounded-lg border border-gray-700 text-red-400 hover:border-red-500">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        `;
        return row;
    }

    btnAdd.addEventListener('click', function () {
        const idx = variationsTable.querySelectorAll('tr.variation-row').length;
        const row = createVariationRow(idx);
        variationsTable.appendChild(row);
        wireRow(row);

        renumberRows();
        checkEmptyTable();
        calculateTotalStock();
    });

    if (btnClearAll) {
        btnClearAll.addEventListener('click', function () {
            if (confirm('Tem certeza que deseja excluir TODAS as variações?')) {
                variationsTable.innerHTML = '';
                renumberRows();
                checkEmptyTable();
                calculateTotalStock();
            }
        });
    }

    // Upload imagem
    if (imagePreview && imageUpload) {
        imagePreview.addEventListener('click', function () {
            imageUpload.click();
        });

        imageUpload.addEventListener('change', function (e) {
            const file = e.target.files?.[0];
            if (!file) return;

            const reader = new FileReader();
            reader.onload = function (ev) {
                imageContainer.innerHTML = `
                    <div class="relative">
                        <img src="${ev.target.result}" class="max-h-64 rounded-xl">
                        <button type="button" class="pf-remove-image absolute top-2 right-2 w-8 h-8 rounded-full flex items-center justify-center" id="removeImage">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                `;

                document.getElementById('removeImage')?.addEventListener('click', function () {
                    imageContainer.innerHTML = '';
                    imageUpload.value = '';
                });
            };

            reader.readAsDataURL(file);
        });
    }

    // Máscara de moeda
    function formatCurrency(value) {
        const n = parseFloat(value);
        if (Number.isNaN(n)) return '0,00';
        return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    document.addEventListener('input', function (e) {
        const t = e.target;
        if (!t) return;

        if (t.classList.contains('variation-price') || t.classList.contains('variation-promo')) {
            let value = t.value.replace(/\D/g, '');
            value = (value / 100).toFixed(2);
            t.value = formatCurrency(value);
        }

        if (t.classList.contains('variation-stock') || t.classList.contains('variation-multiplier') || t.classList.contains('variation-active')) {
            calculateTotalStock();
        }
    });

    // Submit: garante Nome hidden correto
    if (form) {
        form.addEventListener('submit', function () {
            variationsTable.querySelectorAll('tr.variation-row').forEach(row => syncNomeRow(row));
            calculateTotalStock();
        });
    }

    // inicializar linhas existentes
    variationsTable.querySelectorAll('tr.variation-row').forEach(row => wireRow(row));
    renumberRows();
    checkEmptyTable();
    calculateTotalStock();
});
