document.addEventListener('DOMContentLoaded', function () {
    // Máscara para telefone
    $('#telefone').inputmask('(99) 99999-9999');

    // Validação do formulário
    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function (e) {
            const telefone = document.getElementById('telefone').value;
            const digitsOnly = telefone.replace(/\D/g, '');

            if (digitsOnly.length < 11) {
                e.preventDefault();
                alert('Por favor, informe um telefone válido com DDD');
            }
        });
    }

    // Efeito hover nos botões
    const buttons = document.querySelectorAll('.btn');
    buttons.forEach(button => {
        button.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-2px)';
        });
        button.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0)';
        });
    });
});

<script>
    (function(){
  const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

    if(!isIOS) return;

  // Centraliza o campo focado (evita “salto” visual)
  document.addEventListener('focusin', (e) => {
    if (!e.target.matches('input, textarea, select')) return;
    setTimeout(() => {
      try {e.target.scrollIntoView({ block: 'center', inline: 'nearest' }); } catch { }
    }, 140);
  });

    // Se estiver usando bottom-sheet, ajusta com visualViewport
    if (window.visualViewport) {
    const apply = () => {
      const vv = visualViewport;
    const inset = Math.max(0, (window.innerHeight - vv.height - vv.offsetTop));
    document.documentElement.style.setProperty('--vv-bottom', inset + 'px');
    };
    visualViewport.addEventListener('resize', apply);
    visualViewport.addEventListener('scroll', apply);
    apply();
  }
})();
</script>
