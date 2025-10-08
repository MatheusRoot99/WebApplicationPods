(function () {
    function updateTotals() {
        let total = 0;
        document.querySelectorAll('#flavorsBody .qty-input').forEach(i => {
            const n = parseInt(i.value || '0', 10);
            if (!isNaN(n)) total += n;
        });
        const totalSpan = document.getElementById('totalEstoque');
        const estoqueEl = document.getElementById('Estoque');
        if (totalSpan) totalSpan.textContent = total;
        if (estoqueEl) estoqueEl.value = total;
    }

    function bindRowEvents(tr) {
        tr.querySelector('.qty-input')?.addEventListener('input', updateTotals);
        tr.querySelector('.remove-row')?.addEventListener('click', function () {
            tr.remove();
            updateTotals();
        });
    }

    function makeRow(sabor = '', qtd = 0) {
        const tr = document.createElement('tr');
        tr.className = 'flavor-row';
        tr.innerHTML = `
      <td>
        <select class="form-select sabor-select">
          <option value="">(Selecione)</option>
          ${window.SABORES_OPTIONS_HTML || ''}
        </select>
      </td>
      <td>
        <input type="text" class="form-control novo-sabor-input" placeholder="Digite um novo sabor (opcional)" />
        <div class="form-text">Se preencher aqui, o texto digitado será usado.</div>
      </td>
      <td>
        <input type="number" class="form-control qty-input" min="0" value="${qtd}" />
      </td>
      <td class="text-end">
        <button type="button" class="btn btn-outline-danger btn-sm remove-row">
          <i class="fas fa-times"></i>
        </button>
      </td>`;
        document.getElementById('flavorsBody').appendChild(tr);

        // pré-seleciona o sabor se veio preenchido
        if (sabor) {
            const sel = tr.querySelector('.sabor-select');
            // tenta selecionar na lista; se não existir, coloca no input de novo sabor
            if ([...sel.options].some(o => (o.value || '') === sabor)) {
                sel.value = sabor;
            } else {
                tr.querySelector('.novo-sabor-input').value = sabor;
            }
        }

        bindRowEvents(tr);
        updateTotals();
    }

    document.addEventListener('DOMContentLoaded', function () {
        // vincula linhas existentes da view
        document.querySelectorAll('#flavorsBody tr.flavor-row').forEach(tr => bindRowEvents(tr));

        // botão "Adicionar linha"
        document.getElementById('addFlavor')?.addEventListener('click', function () {
            makeRow('', 0);
        });

        // submissão: gera inputs hidden p/ o backend
        document.getElementById('produtoForm')?.addEventListener('submit', function () {
            const hidden = document.getElementById('flavorsHiddenContainer');
            hidden.innerHTML = '';

            document.querySelectorAll('#flavorsBody tr').forEach(tr => {
                const selVal = (tr.querySelector('.sabor-select')?.value || '').trim();
                const novoVal = (tr.querySelector('.novo-sabor-input')?.value || '').trim();
                const sabor = novoVal || selVal; // prioriza o texto digitado

                let qtd = parseInt(tr.querySelector('.qty-input')?.value || '0', 10);
                qtd = isNaN(qtd) ? 0 : qtd;

                if (sabor && qtd > 0) {
                    const inp = document.createElement('input');
                    inp.type = 'hidden';
                    inp.name = 'SaboresQuantidadesList';
                    // serialize com PascalCase para bater 100% com o model
                    inp.value = JSON.stringify({ Sabor: sabor, Quantidade: qtd });
                    hidden.appendChild(inp);
                }
            });

            updateTotals();
        });

        // Ajuste para bottom-nav (se houver)
        const nav = document.querySelector('.mobile-bottom-nav, .bottom-nav, .mbn, .navbar-fixed-bottom');
        const root = document.documentElement;
        function setMbnHeight() {
            const h = nav ? nav.offsetHeight : 68;
            root.style.setProperty('--mbn-height', h + 'px');
        }
        setMbnHeight();
        window.addEventListener('resize', setMbnHeight);
        if (nav && 'ResizeObserver' in window) {
            new ResizeObserver(setMbnHeight).observe(nav);
        }

        updateTotals();
    });
})();
