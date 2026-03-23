/* wwwroot/js/pedido-tracker.js (no-modules, compat ES5) */
(function () {
    function to2(n) { return (n < 10 ? '0' : '') + n; }
    function fmtHM(iso) {
        if (!iso) return '—';
        var d = new Date(iso); if (isNaN(d.getTime())) return '—';
        return to2(d.getHours()) + ':' + to2(d.getMinutes());
    }
    function qs(sel, root) { return (root || document).querySelector(sel); }
    function qsa(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

    function statusToStep(text) {
        var s = (text || '').toLowerCase();

        if (s.indexOf('cancel') >= 0 || s.indexOf('falhou') >= 0) return -1;

        if (s.indexOf('concl') >= 0 || s.indexOf('entregue') >= 0 || s.indexOf('entreg') >= 0) return 5;

        if (
            s.indexOf('rota') >= 0 ||
            s.indexOf('saiu') >= 0 ||
            s.indexOf('retirada') >= 0 ||
            s.indexOf('pronto') >= 0
        ) return 4;

        if (
            s.indexOf('prepar') >= 0 ||
            s.indexOf('produção') >= 0 ||
            s.indexOf('producao') >= 0
        ) return 3;

        if (
            s.indexOf('pago') >= 0 ||
            s.indexOf('aprov') >= 0
        ) return 2;

        if (
            s.indexOf('aguard') >= 0 && s.indexOf('pag') >= 0
        ) return 1;

        return 0;
    }

    // no-op se não existir #statusText
    function setBadge(status) {
        var txt = qs('#statusText');
        if (txt) txt.textContent = status || '—';
    }

    // no-op se não existir #etaText
    function setEta(minutes) {
        var root = qs('#vtrack');
        var chip = qs('#etaText');
        if (!root || !chip) return;
        try {
            var startIso = root.getAttribute('data-start');
            var isDelivery = (root.getAttribute('data-is-delivery') === 'true');
            var base = new Date(startIso);
            var eta = new Date(base.getTime() + (minutes || 50) * 60000);
            chip.textContent = (isDelivery ? 'Entrega' : 'Retirada') + ' prevista ~ ' + to2(eta.getHours()) + ':' + to2(eta.getMinutes());
        } catch (e) { chip.textContent = '—'; }
    }

    function applyStepTimes(times) {
        if (!times) return;
        for (var i = 0; i <= 5; i++) {
            var t = times[i] || times[String(i)] || null;
            var node = qs('.vstep[data-step="' + i + '"] .vtime');
            if (node && t) node.textContent = fmtHM(t);
        }
    }

    // guarda o passo atual para repaints
    var __currentStep = 0;

    // calcula a altura da linha preenchida até o centro da bolinha alvo
    function updateFilledLine(step) {
        var track = qs('#vtrack');
        if (!track) return;
        var target = null;

        if (step <= 0) {
            target = qs('.vstep[data-step="0"] .vbullet', track);
        } else {
            target = qs('.vstep[data-step="' + step + '"] .vbullet', track);
            if (!target) {
                var dones = qsa('.vstep.done .vbullet', track);
                target = dones.length ? dones[dones.length - 1] : null;
            }
        }
        var fill = 0;
        if (target) {
            var rectT = track.getBoundingClientRect();
            var rectB = target.getBoundingClientRect();
            fill = Math.max(0, (rectB.top - rectT.top) + (target.offsetHeight / 2));
        }
        track.style.setProperty('--vtrack-fill', fill + 'px');
    }

    function paintSteps(step) {
        if (typeof step === 'number') __currentStep = step;

        var nodes = qsa('.vstep');
        for (var i = 0; i < nodes.length; i++) {
            nodes[i].classList.remove('done');
            nodes[i].classList.remove('active');
            nodes[i].classList.remove('cancelled');

            if (__currentStep >= 0) {
                if (i < __currentStep) nodes[i].classList.add('done');
                if (i === __currentStep) nodes[i].classList.add('active');
            }
        }

        if (__currentStep === -1 && nodes.length) {
            nodes[0].classList.add('active');
            nodes[0].classList.add('cancelled');
        }

        updateFilledLine(__currentStep >= 0 ? __currentStep : 0);
    }

    function buildNoCacheUrl(url) {
        try {
            var u = new URL(url, window.location.origin);
            u.searchParams.set('_', Date.now().toString());
            return u.toString();
        } catch (e) {
            var sep = url.indexOf('?') >= 0 ? '&' : '?';
            return url + sep + '_=' + Date.now();
        }
    }

    function init(opt) {
        var cfg = opt || {};
        var pollMs = cfg.pollMs || 10000;

        // setEta(cfg.etaMinutes || 50); // desnecessário se o chip não existe
        paintSteps(statusToStep(cfg.initialStatus || ''));

        function refresh() {
            if (!cfg.statusUrl) return;
            var btn = qs('#btnRefresh');
            if (btn) { btn.disabled = true; btn.innerHTML = '<i class="bi bi-arrow-repeat"></i> Verificando...'; }

            var url = buildNoCacheUrl(cfg.statusUrl);
            fetch(url, { cache: 'no-store' })
                .then(function (r) { if (!r.ok) throw new Error('http ' + r.status); return r.json(); })
                .then(function (data) {
                    var status = (data && data.status) ? data.status : '';
                    var step = (data && typeof data.step === 'number') ? data.step : statusToStep(status);
                    var times = (data && data.times) ? data.times : null;

                    // setBadge(status); // opcional; no-op se não houver #statusText
                    paintSteps(step);
                    applyStepTimes(times);
                })
                .catch(function () { })
                .finally(function () {
                    if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-arrow-clockwise"></i> Atualizar agora'; }
                });
        }

        // botão manual
        var btnR = qs('#btnRefresh');
        if (btnR) { btnR.addEventListener('click', refresh); }

        // polling
        var timer = setInterval(refresh, pollMs);
        window.addEventListener('beforeunload', function () { clearInterval(timer); });

        // se o app voltar para foco (aba visível), atualiza na hora
        document.addEventListener('visibilitychange', function () {
            if (document.visibilityState === 'visible') refresh();
        });

        // repinta usando o passo atual, sem depender de #statusText
        function repaintFromCurrent() { paintSteps(__currentStep); }
        window.addEventListener('resize', repaintFromCurrent);
        window.addEventListener('orientationchange', repaintFromCurrent);

        // primeira chamada
        refresh();
    }

    window.PedidoTracker = {
        init: init,
        paintSteps: paintSteps,
        setEta: setEta,
        applyStepTimes: applyStepTimes,
        setBadge: setBadge,
        statusToStep: statusToStep
    };
})();
