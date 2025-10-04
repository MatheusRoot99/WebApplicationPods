// wwwroot/js/cep.js
(function () {
    const $cep = document.getElementById('cep');
    const $logradouro = document.getElementById('logradouro');
    const $bairro = document.getElementById('bairro');
    const $cidade = document.getElementById('cidade');
    const $estado = document.getElementById('estado');
    const $complemento = document.getElementById('complemento');
    const $numero = document.getElementById('numero');

    if (!$cep) return;

    // ========== 1) Máscara do CEP ==========
    try {
        if (window.Inputmask) {
            Inputmask('99999-999', { showMaskOnHover: false }).mask($cep);
        } else {
            // fallback manual
            $cep.addEventListener('input', function (e) {
                let v = e.target.value.replace(/\D+/g, '').slice(0, 8);
                if (v.length > 5) v = v.slice(0, 5) + '-' + v.slice(5);
                e.target.value = v;
            });
        }
    } catch { }

    // ========== 2) Evita Enter/“Ir” enviar o form ==========
    $cep.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); }
    });

    // ========== 3) Dispara busca com debounce ==========
    let t;
    $cep.addEventListener('input', () => {
        clearTimeout(t);
        if (onlyDigits($cep.value).length === 8) t = setTimeout(buscarCEP, 250);
    });
    $cep.addEventListener('blur', () => {
        if (onlyDigits($cep.value).length === 8) buscarCEP();
    });

    function onlyDigits(s) { return (s || '').replace(/\D+/g, ''); }

    // pega o primeiro campo existente respeitando as diferentes APIs (PascalCase/minúsculo)
    function pick(obj, keys) {
        for (const k of keys) {
            if (obj && obj[k] != null && obj[k] !== '') return obj[k];
        }
        return '';
    }

    function setVal(el, v) { if (el) el.value = v ?? ''; }

    function setEndereco(data) {
        // aceita PascalCase (sua API), minúsculo (ViaCEP), e nomes alternativos (BrasilAPI)
        setVal($logradouro, pick(data, ['Logradouro', 'logradouro', 'rua', 'street']));
        setVal($bairro, pick(data, ['Bairro', 'bairro', 'neighborhood']));
        setVal($cidade, pick(data, ['Cidade', 'cidade', 'Localidade', 'localidade', 'city']));
        setVal($estado, (pick(data, ['Estado', 'estado', 'Uf', 'uf', 'state']) + '').toUpperCase());
        setVal($complemento, pick(data, ['Complemento', 'complemento', 'complement']));
        if ($numero) $numero.focus();
    }

    function setLoading(on) {
        const spin = document.querySelector('.loading-spinner');
        const grp = document.querySelector('.cep-group');
        if (spin) spin.style.opacity = on ? '1' : '0';
        if (grp) grp.classList.toggle('loading', !!on);
    }

    function feedback(msg, type) {
        const old = document.querySelector('.cep-feedback');
        if (old) old.remove();
        const div = document.createElement('div');
        div.className = `cep-feedback ${type || 'info'}`;
        div.textContent = msg;
        $cep.insertAdjacentElement('afterend', div);
        setTimeout(() => { div.style.opacity = '0'; setTimeout(() => div.remove(), 300); }, 5000);
    }

    async function buscarCEP() {
        const cep = onlyDigits($cep.value);
        if (cep.length !== 8) {
            feedback('CEP inválido. Deve conter 8 dígitos.', 'error');
            return;
        }

        setLoading(true);

        // 1) Sua API (normalmente PascalCase: Logradouro, Bairro, Localidade, Uf)
        try {
            const r = await fetch(`/api/cep/${cep}`, { headers: { 'Accept': 'application/json' }, cache: 'no-store' });
            if (r.ok) {
                const data = await r.json();
                setEndereco(data);
                feedback('Endereço preenchido com sucesso!', 'success');
                setLoading(false);
                return;
            }
        } catch (e) {
            console.debug('Falha ao consultar /api/cep:', e);
        }

        // 2) ViaCEP (logradouro/bairro/localidade/uf)
        try {
            const r = await fetch(`https://viacep.com.br/ws/${cep}/json/`, { headers: { 'Accept': 'application/json' }, cache: 'no-store' });
            if (r.ok) {
                const d = await r.json();
                if (!d.erro) {
                    setEndereco(d);
                    feedback('Endereço preenchido (ViaCEP).', 'success');
                    setLoading(false);
                    return;
                }
            }
        } catch (e) {
            console.debug('ViaCEP falhou:', e);
        }

        // 3) BrasilAPI (street/neighborhood/city/state)
        try {
            const r = await fetch(`https://brasilapi.com.br/api/cep/v2/${cep}`, { headers: { 'Accept': 'application/json' }, cache: 'no-store' });
            if (r.ok) {
                const d = await r.json();
                setEndereco({ rua: d.street, neighborhood: d.neighborhood, city: d.city, state: d.state, complement: d.complement });
                feedback('Endereço preenchido (BrasilAPI).', 'success');
                return;
            }
        } catch (e) {
            console.debug('BrasilAPI falhou:', e);
        } finally {
            setLoading(false);
        }

        feedback('Não foi possível preencher o endereço. Verifique o CEP.', 'error');
    }
})();
