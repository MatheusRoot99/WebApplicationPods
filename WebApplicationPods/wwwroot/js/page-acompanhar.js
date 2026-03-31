// wwwroot/js/page-acompanhar.js
(function () {
    function initTabs() {
        var tabs = document.getElementById('tabs');
        if (!tabs) return;

        tabs.addEventListener('click', function (e) {
            var seg = e.target.closest('.seg');
            if (!seg) return;

            var which = seg.getAttribute('data-tab');
            if (!which) return;

            Array.prototype.forEach.call(tabs.querySelectorAll('.seg'), function (s) {
                s.classList.remove('active');
            });

            seg.classList.add('active');

            Array.prototype.forEach.call(document.querySelectorAll('.tab'), function (t) {
                t.classList.remove('active');
            });

            var pane = document.getElementById('tab-' + which);
            if (pane) pane.classList.add('active');
        });
    }

    function initTracker() {
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
    }

    function init() {
        initTabs();
        initTracker();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();