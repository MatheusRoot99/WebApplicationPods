using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Options;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class WhatsAppService : IWhatsAppService
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private readonly HttpClient _http;
        private readonly BancoContext _context;
        private readonly IStoreUrlBuilder _storeUrlBuilder;
        private readonly WhatsAppOptions _options;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            HttpClient http,
            BancoContext context,
            IStoreUrlBuilder storeUrlBuilder,
            IOptions<WhatsAppOptions> options,
            ILogger<WhatsAppService> logger)
        {
            _http = http;
            _context = context;
            _storeUrlBuilder = storeUrlBuilder;
            _options = options.Value;
            _logger = logger;
        }

        public async Task EnviarNovoPedidoClienteAsync(PedidoModel pedido)
        {
            var cliente = PrimeiroNome(pedido.Cliente?.Nome, "cliente");
            var total = pedido.ValorTotal.ToString("C", PtBr);
            var link = await MontarLinkAcompanhamentoAsync(pedido);

            var mensagem =
$@"*Pedido recebido* ✅

Olá, {cliente}!
Recebemos o seu pedido *#{pedido.Id}* no valor de *{total}*.

Acompanhe aqui:
{link}";

            await EnviarParaClienteAsync(pedido, "pedido-criado-cliente", mensagem);
        }

        public async Task EnviarNovoPedidoLojistaAsync(PedidoModel pedido)
        {
            var cliente = pedido.Cliente?.Nome ?? $"Cliente #{pedido.ClienteId}";
            var total = pedido.ValorTotal.ToString("C", PtBr);

            var mensagem =
$@"*Novo pedido* 🔔

Pedido *#{pedido.Id}* de {cliente}.
Total: *{total}*
Pagamento: {pedido.MetodoPagamento}.";

            await EnviarParaLojistaAsync(pedido.LojaId, pedido.Id, "pedido-criado-lojista", mensagem);
        }

        public async Task EnviarPagamentoAprovadoClienteAsync(PedidoModel pedido)
        {
            var link = await MontarLinkAcompanhamentoAsync(pedido);

            var mensagem =
$@"*Pagamento aprovado* ✅

O pagamento do pedido *#{pedido.Id}* foi confirmado.

Acompanhe aqui:
{link}";

            await EnviarParaClienteAsync(pedido, "pagamento-aprovado-cliente", mensagem);
        }

        public async Task EnviarPagamentoFalhouClienteAsync(PedidoModel pedido)
        {
            var link = await MontarLinkAcompanhamentoAsync(pedido);

            var mensagem =
$@"*Pagamento não confirmado* ⚠️

Não conseguimos confirmar o pagamento do pedido *#{pedido.Id}*.

Confira o pedido aqui:
{link}";

            await EnviarParaClienteAsync(pedido, "pagamento-falhou-cliente", mensagem);
        }

        public async Task EnviarPedidoCanceladoClienteAsync(PedidoModel pedido)
        {
            var mensagem =
$@"*Pedido cancelado* ❌

O pedido *#{pedido.Id}* foi cancelado.
Se precisar, entre em contato com a loja.";

            await EnviarParaClienteAsync(pedido, "pedido-cancelado-cliente", mensagem);
        }

        public async Task EnviarSaiuParaEntregaClienteAsync(PedidoModel pedido)
        {
            var link = await MontarLinkAcompanhamentoAsync(pedido);

            var entregadorNome = pedido.Entregador?.Nome;
            var entregadorTelefone = FormatarTelefoneMensagem(
                !string.IsNullOrWhiteSpace(pedido.Entregador?.Telefone)
                    ? pedido.Entregador!.Telefone
                    : pedido.Entregador?.Usuario?.PhoneNumber);

            var blocoEntregador = string.IsNullOrWhiteSpace(entregadorNome)
                ? string.Empty
                : $"\nEntregador: {entregadorNome}" +
                  (string.IsNullOrWhiteSpace(entregadorTelefone) ? "" : $" ({entregadorTelefone})");

            var mensagem =
$@"*Saiu para entrega* 🚚

O pedido *#{pedido.Id}* saiu para entrega.{blocoEntregador}

Acompanhe aqui:
{link}";

            await EnviarParaClienteAsync(pedido, "saiu-para-entrega-cliente", mensagem);
        }

        public async Task EnviarPedidoEntregueClienteAsync(PedidoModel pedido)
        {
            var textoFinal = pedido.RetiradaNoLocal
                ? "foi finalizado com sucesso."
                : "foi entregue com sucesso.";

            var mensagem =
$@"*Pedido concluído* 📦

O pedido *#{pedido.Id}* {textoFinal}

Obrigado pela preferência!";

            await EnviarParaClienteAsync(pedido, "pedido-entregue-cliente", mensagem);
        }

        public async Task EnviarEntregaAtribuidaEntregadorAsync(PedidoModel pedido, EntregadorModel entregador)
        {
            var cliente = pedido.Cliente?.Nome ?? $"Cliente #{pedido.ClienteId}";
            var telefoneCliente = FormatarTelefoneMensagem(pedido.Cliente?.Telefone);
            var endereco = MontarResumoEndereco(pedido);

            var mensagem =
$@"*Nova entrega atribuída* 🚚

Pedido *#{pedido.Id}*
Cliente: {cliente}
Telefone: {telefoneCliente}
Destino: {endereco}";

            await EnviarParaEntregadorAsync(entregador, pedido.LojaId, pedido.Id, "entrega-atribuida-entregador", mensagem);
        }

        public async Task EnviarEntregaAceitaLojistaAsync(PedidoModel pedido)
        {
            var entregador = pedido.Entregador?.Nome ?? "Entregador";

            var mensagem =
$@"*Entrega aceita* ✅

{entregador} aceitou o pedido *#{pedido.Id}*.";

            await EnviarParaLojistaAsync(pedido.LojaId, pedido.Id, "entrega-aceita-lojista", mensagem);
        }

        public async Task EnviarEntregaConcluidaLojistaAsync(PedidoModel pedido, string? nomeRecebedor = null)
        {
            var recebedor = string.IsNullOrWhiteSpace(nomeRecebedor) ? "não informado" : nomeRecebedor.Trim();

            var mensagem =
$@"*Entrega concluída* 📦

Pedido *#{pedido.Id}* entregue.
Recebido por: {recebedor}.";

            await EnviarParaLojistaAsync(pedido.LojaId, pedido.Id, "entrega-concluida-lojista", mensagem);
        }

        public async Task EnviarFalhaEntregaLojistaAsync(PedidoModel pedido, string motivo)
        {
            var mensagem =
$@"*Falha na entrega* ⚠️

Pedido *#{pedido.Id}* não foi entregue.
Motivo: {motivo}";

            await EnviarParaLojistaAsync(pedido.LojaId, pedido.Id, "falha-entrega-lojista", mensagem);
        }

        public async Task<bool> EnviarMensagemLivreAsync(
            string telefone,
            string mensagem,
            string audience = "teste",
            string? eventKey = "manual-teste")
        {
            if (string.IsNullOrWhiteSpace(telefone) || string.IsNullOrWhiteSpace(mensagem))
                return false;

            return await EnviarCoreAsync(
                telefone,
                audience,
                eventKey ?? "manual-teste",
                mensagem.Trim(),
                lojaId: 0,
                pedidoId: null);
        }

        private async Task EnviarParaClienteAsync(PedidoModel pedido, string eventKey, string mensagem)
        {
            if (!_options.SendToCustomer)
                return;

            await EnviarCoreAsync(
                pedido.Cliente?.Telefone,
                "cliente",
                eventKey,
                mensagem,
                pedido.LojaId,
                pedido.Id);
        }

        private async Task EnviarParaEntregadorAsync(
            EntregadorModel entregador,
            int lojaId,
            int pedidoId,
            string eventKey,
            string mensagem)
        {
            if (!_options.SendToEntregador)
                return;

            var telefone = !string.IsNullOrWhiteSpace(entregador.Telefone)
                ? entregador.Telefone
                : entregador.Usuario?.PhoneNumber;

            await EnviarCoreAsync(
                telefone,
                "entregador",
                eventKey,
                mensagem,
                lojaId,
                pedidoId);
        }

        private async Task EnviarParaLojistaAsync(
            int lojaId,
            int pedidoId,
            string eventKey,
            string mensagem)
        {
            if (!_options.SendToLojista)
                return;

            var telefone = await ObterTelefoneLojistaAsync(lojaId);

            await EnviarCoreAsync(
                telefone,
                "lojista",
                eventKey,
                mensagem,
                lojaId,
                pedidoId);
        }

        private async Task<bool> EnviarCoreAsync(
            string? telefoneBruto,
            string audience,
            string eventKey,
            string mensagem,
            int lojaId,
            int? pedidoId)
        {
            var destino = NormalizarTelefone(telefoneBruto);

            if (string.IsNullOrWhiteSpace(destino))
            {
                _logger.LogDebug(
                    "WhatsApp ignorado: sem telefone válido. Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}",
                    audience, eventKey, lojaId, pedidoId);
                return false;
            }

            if (!_options.Enabled)
            {
                _logger.LogDebug(
                    "WhatsApp desabilitado. Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}",
                    audience, eventKey, lojaId, pedidoId);
                return false;
            }

            if (string.Equals(_options.Mode, "MetaCloudApi", StringComparison.OrdinalIgnoreCase))
            {
                return await EnviarViaMetaCloudApiAsync(
                    destino,
                    audience,
                    eventKey,
                    mensagem,
                    lojaId,
                    pedidoId);
            }

            _logger.LogInformation(
                "[WhatsApp:Stub] Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}, To={To}, Message={Message}",
                audience, eventKey, lojaId, pedidoId, MascararTelefone(destino), mensagem);

            return true;
        }

        private async Task<bool> EnviarViaMetaCloudApiAsync(
            string destino,
            string audience,
            string eventKey,
            string mensagem,
            int lojaId,
            int? pedidoId)
        {
            var phoneNumberId = _options.MetaPhoneNumberId?.Trim();
            var accessToken = _options.MetaAccessToken?.Trim();
            var apiVersion = (_options.MetaApiVersion ?? "v25.0").Trim();

            if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning(
                    "Meta Cloud API não configurada. Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}",
                    audience, eventKey, lojaId, pedidoId);
                return false;
            }

            var endpoint = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = JsonContent.Create(new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = destino,
                    type = "text",
                    text = new
                    {
                        preview_url = false,
                        body = mensagem
                    }
                });

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "WhatsApp enviado via Meta Cloud API. Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}, To={To}, Response={ResponseBody}",
                        audience, eventKey, lojaId, pedidoId, MascararTelefone(destino), body);
                    return true;
                }

                _logger.LogWarning(
                    "Falha ao enviar WhatsApp via Meta Cloud API. Status={StatusCode}, Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}, To={To}, Body={Body}",
                    (int)response.StatusCode,
                    audience,
                    eventKey,
                    lojaId,
                    pedidoId,
                    MascararTelefone(destino),
                    body);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erro ao enviar WhatsApp via Meta Cloud API. Audience={Audience}, Event={Event}, LojaId={LojaId}, PedidoId={PedidoId}, To={To}",
                    audience,
                    eventKey,
                    lojaId,
                    pedidoId,
                    MascararTelefone(destino));

                return false;
            }
        }

        private async Task<string?> ObterTelefoneLojistaAsync(int lojaId)
        {
            var loja = await _context.Lojas
                .AsNoTracking()
                .Include(x => x.Dono)
                .FirstOrDefaultAsync(x => x.Id == lojaId);

            if (!string.IsNullOrWhiteSpace(loja?.Dono?.PhoneNumber))
                return loja.Dono.PhoneNumber;

            var config = await _context.LojaConfigs
                .AsNoTracking()
                .Include(x => x.Lojista)
                .FirstOrDefaultAsync(x => x.LojaId == lojaId);

            return config?.Lojista?.PhoneNumber;
        }

        private async Task<string> MontarLinkAcompanhamentoAsync(PedidoModel pedido)
        {
            var subdominio = await _context.Lojas
                .AsNoTracking()
                .Where(x => x.Id == pedido.LojaId)
                .Select(x => x.Subdominio)
                .FirstOrDefaultAsync();

            var baseUrl = _storeUrlBuilder.BuildPublicStoreUrl(subdominio ?? string.Empty).TrimEnd('/');
            var url = $"{baseUrl}/Pedido/Acompanhar/{pedido.Id}";

            if (!string.IsNullOrWhiteSpace(pedido.RastreioToken))
                url += $"?t={Uri.EscapeDataString(pedido.RastreioToken)}";

            return url;
        }

        private static string MontarResumoEndereco(PedidoModel pedido)
        {
            if (pedido.RetiradaNoLocal)
            {
                return string.IsNullOrWhiteSpace(pedido.LojaEnderecoTexto)
                    ? "Retirada na loja"
                    : pedido.LojaEnderecoTexto!;
            }

            var e = pedido.Endereco;
            if (e == null)
                return "Endereço não informado";

            var partes = new[]
            {
                string.Join(", ", new[] { e.Logradouro, e.Numero }.Where(x => !string.IsNullOrWhiteSpace(x))),
                e.Complemento,
                e.Bairro,
                string.Join("/", new[] { e.Cidade, e.Estado }.Where(x => !string.IsNullOrWhiteSpace(x)))
            };

            return string.Join(" - ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private string? NormalizarTelefone(string? telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone))
                return null;

            var digits = new string(telefone.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return null;

            var ddi = new string((_options.DefaultCountryCode ?? "55").Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(ddi))
                ddi = "55";

            if (digits.StartsWith(ddi) && digits.Length >= ddi.Length + 10)
                return digits;

            if (digits.Length == 10 || digits.Length == 11)
                return ddi + digits;

            return digits;
        }

        private static string FormatarTelefoneMensagem(string? telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone))
                return "-";

            var digits = new string(telefone.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("55") && digits.Length >= 12)
                digits = digits[2..];

            if (digits.Length == 11)
                return $"({digits[..2]}) {digits.Substring(2, 5)}-{digits.Substring(7, 4)}";

            if (digits.Length == 10)
                return $"({digits[..2]}) {digits.Substring(2, 4)}-{digits.Substring(6, 4)}";

            return digits;
        }

        private static string PrimeiroNome(string? nomeCompleto, string fallback)
        {
            if (string.IsNullOrWhiteSpace(nomeCompleto))
                return fallback;

            return nomeCompleto.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? fallback;
        }

        private static string MascararTelefone(string telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone) || telefone.Length <= 4)
                return telefone;

            return new string('*', telefone.Length - 4) + telefone[^4..];
        }
    }
}