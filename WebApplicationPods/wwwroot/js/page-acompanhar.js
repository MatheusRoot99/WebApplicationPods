// wwwroot/js/page-acompanhar.js
(function () {
    // Tabs
    var tabs = document.getElementById('tabs');
    if (tabs) {
        tabs.addEventListener('click', function (e) {
            var seg = e.target.closest('.seg'); if (!seg) return;
            Array.prototype.forEach.call(tabs.querySelectorAll('.seg'), function (s) { s.classList.remove('active'); });
            seg.classList.add('active');
            var which = seg.getAttribute('data-tab');
            Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (t) { t.classList.remove('active'); });
            var pane = document.getElementById('tab-' + which);
            if (pane) pane.classList.add('active');
        });
    }

    // Init tracker (lendo data-attrs)
    var data = document.getElementById('trackData');
    if (!data || !window.PedidoTracker) return;

    var statusUrl = data.getAttribute('data-status-url') || '';
    var initialStatus = data.getAttribute('data-initial-status') || '';

    window.PedidoTracker.init({
        statusUrl: statusUrl,
        initialStatus: initialStatus,
        etaMinutes: 50,
        pollMs: 10000
    });
})();


// wwwroot/js/page-acompanhar.js
(function () {
    console.log('page-acompanhar.js carregado');

    // Tabs
    var tabs = document.getElementById('tabs');
    if (tabs) {
        tabs.addEventListener('click', function (e) {
            var seg = e.target.closest('.seg'); if (!seg) return;
            Array.prototype.forEach.call(tabs.querySelectorAll('.seg'), function (s) { s.classList.remove('active'); });
            seg.classList.add('active');
            var which = seg.getAttribute('data-tab');
            Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (t) { t.classList.remove('active'); });
            var pane = document.getElementById('tab-' + which);
            if (pane) pane.classList.add('active');
        });
    }

    // Init tracker (lendo data-attrs)
    var data = document.getElementById('trackData');
    console.log('Elemento trackData encontrado:', data);

    if (!data || !window.PedidoTracker) {
        console.log('trackData não encontrado ou PedidoTracker não disponível');
        return;
    }

    var statusUrl = data.getAttribute('data-status-url') || '';
    var initialStatus = data.getAttribute('data-initial-status') || '';

    console.log('Inicializando tracker com:', { statusUrl, initialStatus });

    window.PedidoTracker.init({
        statusUrl: statusUrl,
        initialStatus: initialStatus,
        etaMinutes: 50,
        pollMs: 10000
    });
})();
