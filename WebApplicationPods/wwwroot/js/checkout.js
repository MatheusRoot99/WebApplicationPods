/* wwwroot/js/checkout.js */

/* ===== Helpers de DOM ===== */
function $(id) { return document.getElementById(id); }

/* ===== Estado global (preenchido pela view via window.CHECKOUT_CONFIG) ===== */
const CFG = window.CHECKOUT_CONFIG || {};
let COUNTDOWN_INTERVAL = null;
let POLL_INTERVAL_ID = null;
let REDIRECTED = false;

/* ===== Atualiza badge do carrinho ===== */
async function refreshCartBadge() {
    try {
        const r = await fetch('/Carrinho/Count', { cache: 'no-store' });
        if (!r.ok) return;
        const { count } = await r.json();
        const badge = document.querySelector('[data-cart-badge]');
        if (!badge) return;
        badge.textContent = count > 0 ? count : '';
        badge.classList.toggle('show', count > 0);
        // Se seu tema usa "hidden" no lugar da classe:
        // badge.hidden = !(count > 0);
    } catch { /* silencia */ }
}
window.refreshCartBadge = refreshCartBadge;

/* ===== UI: status bar/badge + botões ===== */
function setUiStatus(status) {
    const badge = $('statusBadge');
    const fill = $('statusFill');
    const track = $('goTrackBtn');
    const trackSide = $('btn-acompanhar-pedido');

    const s = (status || '').toLowerCase();
    let pct = 10, cls = 'status-badge', txt = 'Status: ' + status;

    if (s === 'created') { pct = 20; cls += ' status-action'; }
    else if (s === 'requiresaction') { pct = 35; cls += ' status-action'; txt = 'Status: Aguardando ação'; }
    else if (s === 'pending' || s === 'in_process') { pct = 60; cls += ' status-pending'; txt = 'Status: Pendente'; }
    else if (s === 'paid' || s === 'approved') { pct = 100; cls += ' status-paid'; txt = 'Status: Pago'; }
    else if (s === 'failed' || s === 'rejected') { pct = 100; cls += ' status-failed'; txt = 'Status: Falhou'; }
    else if (s === 'canceled' || s === 'cancelled') { pct = 100; cls += ' status-failed'; txt = 'Status: Cancelado'; }

    if (badge) { badge.className = cls; badge.textContent = txt; }
    if (fill) { fill.style.width = pct + '%'; }

    // Mostra botões "Acompanhar pedido" somente quando pago
    const paid = (s === 'paid' || s === 'approved');
    if (track) track.classList.toggle('d-none', !paid);
    if (trackSide) trackSide.classList.toggle('d-none', !paid);

    // Se pago, pare contadores/polling
    if (paid) {
        if (COUNTDOWN_INTERVAL) { clearInterval(COUNTDOWN_INTERVAL); COUNTDOWN_INTERVAL = null; }
        if (POLL_INTERVAL_ID) { clearInterval(POLL_INTERVAL_ID); POLL_INTERVAL_ID = null; }
    }
}

/* ===== Copiar PIX ===== */
async function copyPix() {
    const input = $('pixCopyPaste');
    const btn = $('btnCopyPix');
    const badge = $('copyBadge');
    if (!input) return;

    let ok = false;
    try { await navigator.clipboard.writeText(input.value); ok = true; }
    catch {
        try { input.select(); input.setSelectionRange(0, 99999); document.execCommand('copy'); ok = true; } catch { }
    }

    if (ok && badge) {
        badge.classList.remove('d-none'); void badge.offsetWidth; badge.classList.add('show');
        const old = btn ? btn.textContent : '';
        if (btn) btn.textContent = 'Copiado!';
        setTimeout(() => {
            badge.classList.remove('show');
            setTimeout(() => {
                badge.classList.add('d-none');
                if (btn) btn.textContent = old || 'Copiar';
            }, 200);
        }, 2000);
    }
}
window.copyPix = copyPix; // deixa global para o onclick do botão

/* ===== Countdown (PIX) ===== */
function startCountdown(seconds) {
    const wrap = $('pixCountdown');
    const label = $('pixCountdownTimer');

    if (!wrap || !label) return;

    if (COUNTDOWN_INTERVAL) { clearInterval(COUNTDOWN_INTERVAL); COUNTDOWN_INTERVAL = null; }

    if (!seconds || seconds <= 0) {
        wrap.classList.add('d-none');
        label.textContent = '';
        return;
    }

    wrap.classList.remove('d-none');

    function tick() {
        if (seconds <= 0) {
            clearInterval(COUNTDOWN_INTERVAL);
            COUNTDOWN_INTERVAL = null;
            label.textContent = '00:00';
            // força nova checagem — backend já cancela por expiração
            if (typeof window.__checkPaid === 'function') window.__checkPaid();
            return;
        }
        const mm = String(Math.floor(seconds / 60)).padStart(2, '0');
        const ss = String(seconds % 60).padStart(2, '0');
        label.textContent = `${mm}:${ss}`;
        seconds -= 1;
    }

    tick();
    COUNTDOWN_INTERVAL = setInterval(tick, 1000);
}

/* ===== Polling de status ===== */
function initPolling() {
    const statusUrl = CFG.statusUrl;
    if (!statusUrl) return;

    async function checkPaid() {
        if (REDIRECTED) return;
        try {
            const r = await fetch(statusUrl, { cache: 'no-store' });
            if (!r.ok) return;
            const data = await r.json();

            if (data && data.status) setUiStatus(data.status);

            // countdown (PIX)
            if (typeof data.remainingSeconds === 'number') {
                startCountdown(data.remainingSeconds);
            }

            if (data.paid === true) {
                // carrinho já foi limpo no back; atualize o badge imediatamente
                await refreshCartBadge();

                if (data.redirect && !REDIRECTED) {
                    REDIRECTED = true;
                    window.location.href = data.redirect;
                    return;
                }
            }
        } catch { /* silencia erros */ }
    }

    const btn = $('btnForcarConsulta');
    if (btn && !btn.dataset.bound) {
        btn.dataset.bound = '1';
        btn.addEventListener('click', checkPaid);
    }

    setUiStatus(CFG.initialStatus || 'Created');
    checkPaid();
    POLL_INTERVAL_ID = setInterval(checkPaid, 3000);
    window.__checkPaid = checkPaid;
}

/* ===== QRCode fallback (quando não veio Base64 do provedor) ===== */
function mountQrIfNeeded() {
    const el = $('pix-qr');
    const copyInput = $('pixCopyPaste');
    if (!el || !copyInput) return;
    const brcode = copyInput.value || '';
    if (!brcode || typeof QRCode === 'undefined') return;

    try {
        el.textContent = '';
        new QRCode(el, { text: brcode, width: 240, height: 240 });
    } catch (e) { console.warn('QR fallback falhou', e); }
}

/* ===== MP Brick (cartão) ===== */
function initMPBrick() {
    const publicKey = (CFG.mpPublicKey || '').trim();
    if (!publicKey || (publicKey.indexOf('TEST-') !== 0 && publicKey.indexOf('APP_USR-') !== 0)) {
        // Sem MP key ou inválida — apenas ignora
        return;
    }
    if (window.__mpBrickMounted) return;
    window.__mpBrickMounted = true;

    const hasCardContainer = $('cardPaymentBrick_container');
    if (!hasCardContainer || typeof MercadoPago === 'undefined') return;

    const mp = new MercadoPago(publicKey, { locale: 'pt-BR' });
    const bricks = mp.bricks();

    // Valor já está no HTML (Model.Amount); Brick precisa dele apenas para exibir
    const amountStr = (document.querySelector('[data-amount]')?.getAttribute('data-amount')) ||
        (CFG.amountStr || '');
    const amount = Number(amountStr || '0');

    bricks.create('cardPayment', 'cardPaymentBrick_container', {
        initialization: { amount },
        customization: {
            paymentMethods: { creditCard: 'all', debitCard: 'all' },
            visual: { style: { theme: 'default' } }
        },
        callbacks: {
            onReady: () => { },
            onSubmit: async (cardFormData) => {
                try {
                    const res = await fetch(CFG.confirmCardUrl, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': CFG.antiForgery || ''
                        },
                        body: JSON.stringify(cardFormData)
                    });

                    const json = await res.json();
                    if (json && json.success) {
                        setUiStatus('paid');

                        // badge do carrinho reflete limpeza no servidor
                        await refreshCartBadge();

                        if (json.redirect && !REDIRECTED) {
                            REDIRECTED = true;
                            window.location.href = json.redirect;
                        } else if (window.__checkPaid) {
                            window.__checkPaid();
                        }
                    } else {
                        setUiStatus('failed');
                        alert('Não foi possível processar o pagamento.');
                    }
                } catch (e) {
                    setUiStatus('failed');
                    console.error('ConfirmCard error', e);
                    alert('Erro ao confirmar o pagamento.');
                }
            },
            onError: (error) => {
                console.error('Brick error:', error);
                alert('Erro ao carregar/validar os campos do cartão.');
            }
        }
    });
}

/* ===== Cancelar (PIX e Cartão) ===== */
function bindCancel() {
    const ids = ['mp-cancel-btn', 'pix-cancel-btn'];
    ids.map($).filter(Boolean).forEach(btn => {
        if (btn.dataset.bound) return;
        btn.dataset.bound = '1';
        btn.addEventListener('click', async () => {
            try {
                const r = await fetch(CFG.cancelUrl, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': CFG.antiForgery || '' }
                });
                const data = await r.json().catch(() => ({}));
                if (data && data.redirect) window.location.href = data.redirect;
                else window.location.href = CFG.cartUrl || '/Carrinho/Index';
            } catch {
                window.location.href = CFG.cartUrl || '/Carrinho/Index';
            }
        });
    });
}

/* ===== Boot ===== */
document.addEventListener('DOMContentLoaded', () => {
    try {
        initPolling();
        mountQrIfNeeded();
        initMPBrick();
        bindCancel();
    } catch (e) { console.error(e); }
});
