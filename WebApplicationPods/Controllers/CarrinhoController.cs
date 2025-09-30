
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WebApplicationPods.Enum;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services;
using System.Linq;

namespace WebApplicationPods.Controllers
{
    public class CarrinhoController : Controller
    {
        private readonly ICarrinhoRepository _carrinhoRepository;
        private readonly IProdutoRepository _produtoRepository;
        private readonly IClienteRepository _clienteRepository;
        private readonly IPedidoRepository _pedidoRepository;
        private readonly IClienteRememberService _remember;
        private readonly ILojaConfigRepository _lojaRepo;
        private readonly IHubContext<PedidosHub> _hub;

        public CarrinhoController(
            ICarrinhoRepository carrinhoRepository,
            IProdutoRepository produtoRepository,
            IClienteRepository clienteRepository,
            IPedidoRepository pedidoRepository,
            IClienteRememberService remember,
            ILojaConfigRepository lojaRepo,
            IHubContext<PedidosHub> hub)
        {
            _carrinhoRepository = carrinhoRepository;
            _produtoRepository = produtoRepository;
            _clienteRepository = clienteRepository;
            _pedidoRepository = pedidoRepository;
            _remember = remember;
            _lojaRepo = lojaRepo;
            _hub = hub;
        }

        // =================== Helpers ===================

        private void SetLastOrderCookie(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            var opts = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = false,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            };
            Response.Cookies.Append("last_order_token", token!, opts);
        }

        private int ObterEstoqueDisponivel(ProdutoModel produto, string? sabor)
        {
            if (produto.SaboresQuantidadesList?.Any() == true && !string.IsNullOrWhiteSpace(sabor))
            {
                var sq = produto.SaboresQuantidadesList
                    .FirstOrDefault(s => s.Sabor.Equals(sabor, StringComparison.OrdinalIgnoreCase));
                return sq?.Quantidade ?? 0;
            }
            return produto.Estoque;
        }

        private bool ValidarEstoqueAoAdicionar(ProdutoModel produto, int novaQtd, string sabor, out string mensagemErro)
        {
            mensagemErro = string.Empty;

            var disponivel = ObterEstoqueDisponivel(produto, sabor);
            var carrinho = _carrinhoRepository.ObterCarrinho();
            var chaveSabor = sabor ?? string.Empty;

            var existente = carrinho.Itens
                .Where(i => i.Produto.Id == produto.Id && (i.Sabor ?? string.Empty) == chaveSabor)
                .Sum(i => i.Quantidade);

            if (existente + novaQtd > disponivel)
            {
                var resto = Math.Max(disponivel - existente, 0);
                mensagemErro = string.IsNullOrWhiteSpace(sabor)
                    ? $"Você já tem {existente}. Só é possível adicionar mais {resto} (estoque total {disponivel})."
                    : $"Você já tem {existente} do sabor {sabor}. Só é possível adicionar mais {resto} (estoque total {disponivel}).";
                return false;
            }
            return true;
        }

        private PaymentMethod? MapearMetodoPagamento(string? metodo)
        {
            if (string.IsNullOrWhiteSpace(metodo)) return null;
            metodo = metodo.Trim().ToLowerInvariant();

            return metodo switch
            {
                "dinheiro" => PaymentMethod.Cash,
                "pix" => PaymentMethod.Pix,
                "cartão de crédito" or "cartao de credito" => PaymentMethod.CardCredit,
                "cartão de débito" or "cartao de debito" => PaymentMethod.CardDebit,
                _ => (PaymentMethod?)null
            };
        }

        private decimal CalcularTaxaEntrega(EnderecoModel endereco) => 5m;

        private static int Idade(DateTime nascimento)
        {
            var hoje = DateTime.Today;
            var idade = hoje.Year - nascimento.Year;
            if (nascimento.Date > hoje.AddYears(-idade)) idade--;
            return idade;
        }

        private static bool CarrinhoRequerMaioridade(CarrinhoModel carrinho)
            => carrinho.Itens.Any(i => i.Produto?.RequerMaioridade == true);

        // =================== Actions ===================

        public IActionResult Index()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();

            var populares = _produtoRepository
                .ObterMaisPopulares(8)
                .Select(p => new ProdutoResumoVM
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    ImagemUrl = string.IsNullOrWhiteSpace(p.ImagemUrl)
                        ? "https://via.placeholder.com/600x600?text=%20"
                        : p.ImagemUrl,
                    Preco = p.Preco,
                    PrecoPromocional = p.PrecoPromocional
                })
                .ToList();

            var vm = new CarrinhoPageViewModel
            {
                Carrinho = carrinho,
                Populares = populares
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult GetCarrinhoPartial()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            return PartialView("_CarrinhoTablePartial", carrinho);
        }

        public IActionResult Resumo()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            if (carrinho == null || !carrinho.Itens.Any())
            {
                TempData["Erro"] = "Seu carrinho está vazio.";
                return RedirectToAction("Index");
            }

            var telefone = HttpContext.Session.GetString("ClienteTelefone");
            if (string.IsNullOrWhiteSpace(telefone))
            {
                TempData["ReturnUrl"] = Url.Action(nameof(Resumo), "Carrinho");
                return RedirectToAction("Login", "Auth");
            }

            var cliente = _clienteRepository.ObterPorTelefone(telefone);
            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                TempData["ReturnUrl"] = Url.Action(nameof(Resumo), "Carrinho");
                return RedirectToAction("Login", "Auth");
            }

            // Cinturão 18+: se o carrinho tiver itens que exigem 18+, valide cadastro/idade
            if (CarrinhoRequerMaioridade(carrinho))
            {
                if (!cliente.DataNascimento.HasValue || Idade(cliente.DataNascimento.Value) < 18)
                {
                    TempData["Erro"] = "Há itens que exigem idade mínima de 18 anos. Atualize seus dados para continuar.";
                    return RedirectToAction("Editar", "Auth", new { returnUrl = Url.Action(nameof(Resumo), "Carrinho") });
                }
            }

            var enderecos = _clienteRepository.ObterEnderecos(cliente.Id)?.ToList()
                           ?? new List<EnderecoModel>();

            int? novoId = null;
            if (TempData.ContainsKey("EnderecoNovoId") &&
                int.TryParse(Convert.ToString(TempData["EnderecoNovoId"]), out var parsed))
                novoId = parsed;

            var enderecoSelecionado =
                (novoId.HasValue ? enderecos.FirstOrDefault(e => e.Id == novoId.Value) : null)
                ?? enderecos.FirstOrDefault(e => e.Principal);

            // Envia a loja para a View (box de retirada)
            ViewBag.Loja = _lojaRepo.ObterDoLojistaAtual();
            var skipConfirm = string.Equals(Convert.ToString(TempData["SkipConfirm"]), "1", StringComparison.Ordinal);
            var vm = new ResumoPedidoViewModel
            {
                Carrinho = carrinho,
                Cliente = cliente,
                EnderecosDisponiveis = enderecos,
                EnderecoSelecionadoId = enderecoSelecionado?.Id ?? 0,
                EnderecoEntrega = enderecoSelecionado,
                Observacoes = "",
                RetiradaNoLocal = false,
                PrecisaConfirmar = !skipConfirm, // só confirma se NÃO vier de add/edit
                ReturnUrl = Url.Action(nameof(Resumo), "Carrinho")
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarDados()
        {
            HttpContext.Session.SetString("ClienteConfirmado", "1");

            if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase))
                return Ok();

            return RedirectToAction(nameof(Resumo));
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Confirmacao(int id)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
            {
                TempData["Erro"] = "Pedido não encontrado.";
                return RedirectToAction("Index");
            }

            // Se o pedido já está "Pago", zera o carrinho desta sessão uma única vez
            if (string.Equals(pedido.Status, "Pago", StringComparison.OrdinalIgnoreCase))
            {
                var flagKey = $"CartClearedForOrder_{id}";
                var already = HttpContext.Session.GetString(flagKey);
                if (!string.Equals(already, "1", StringComparison.Ordinal))
                {
                    _carrinhoRepository.LimparCarrinho();
                    HttpContext.Session.SetString(flagKey, "1");
                }
            }

            ViewBag.PedidoId = pedido.Id;
            return View(pedido);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdicionarItem(int produtoId, int quantidade, string? sabor = null,
                                           string? observacoes = null, bool buyNow = false)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
                             StringComparison.OrdinalIgnoreCase);

            try
            {
                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Produto não encontrado." });
                    TempData["Erro"] = "Produto não encontrado.";
                    return RedirectToAction("Index", "Home");
                }

                produto.DeserializarSaboresQuantidades();

                if (!ValidarEstoqueAoAdicionar(produto, quantidade, sabor ?? string.Empty, out var mensagemErro))
                {
                    if (isAjax) return Json(new { ok = false, error = mensagemErro });
                    TempData["Erro"] = mensagemErro;
                    return RedirectToAction("Detalhes", "Produto", new { id = produtoId });
                }

                _carrinhoRepository.AdicionarItem(produto, quantidade, sabor, observacoes);

                var carrinho = _carrinhoRepository.ObterCarrinho();
                var count = carrinho?.Itens?.Sum(i => i.Quantidade) ?? 0;

                if (isAjax) return Json(new { ok = true, count, nome = produto.Nome, buyNow });

                TempData["Sucesso"] = "Adicionado ao carrinho!";
                return buyNow ? RedirectToAction("Resumo", "Carrinho")
                              : RedirectToAction("Index", "Home");
            }
            catch
            {
                if (isAjax) return Json(new { ok = false, error = "Erro ao adicionar ao carrinho." });
                TempData["Erro"] = "Ocorreu um erro ao adicionar o produto ao carrinho.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Count()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            var count = carrinho?.Itens?.Sum(i => i.Quantidade) ?? 0;
            return Json(new { count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AtualizarItem(int produtoId, int? quantidade, string? sabor, string? op)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
                             StringComparison.OrdinalIgnoreCase);

            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var chaveSabor = sabor ?? string.Empty;

                var item = carrinho.Itens.FirstOrDefault(i =>
                    i.Produto.Id == produtoId &&
                    (i.Sabor ?? string.Empty) == chaveSabor);

                if (item == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Item não encontrado no carrinho." });
                    TempData["Erro"] = "Item não encontrado no carrinho.";
                    return RedirectToAction(nameof(Index));
                }

                var novaQtd = item.Quantidade;
                if (string.Equals(op, "inc", StringComparison.OrdinalIgnoreCase)) novaQtd = item.Quantidade + 1;
                else if (string.Equals(op, "dec", StringComparison.OrdinalIgnoreCase)) novaQtd = item.Quantidade - 1;
                else if (quantidade.HasValue) novaQtd = quantidade.Value;

                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Produto não encontrado." });
                    TempData["Erro"] = "Produto não encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                produto.DeserializarSaboresQuantidades();

                var maxPermitido = ObterEstoqueDisponivel(produto, chaveSabor);

                // 🔒 Sem estoque: remover o item (não forçar 1)
                if (maxPermitido < 1)
                {
                    carrinho.Itens.Remove(item);
                    _carrinhoRepository.SalvarCarrinho(carrinho);

                    var isEmptyNow = !carrinho.Itens.Any();
                    var totalNow = carrinho.Total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

                    if (isAjax) return Json(new
                    {
                        ok = false,
                        removed = true,
                        isEmpty = isEmptyNow,
                        total = totalNow,
                        error = "Este item ficou sem estoque e foi removido do carrinho."
                    });

                    TempData["Erro"] = "Este item ficou sem estoque e foi removido do carrinho.";
                    return RedirectToAction(nameof(Index));
                }

                if (novaQtd < 1) novaQtd = 1;
                if (novaQtd > maxPermitido)
                {
                    novaQtd = maxPermitido;
                    if (isAjax) return Json(new { ok = false, error = $"Quantidade ajustada ao máximo disponível ({maxPermitido})." });
                    TempData["Erro"] = $"Quantidade ajustada ao máximo disponível ({maxPermitido}).";
                }

                item.Quantidade = novaQtd;
                _carrinhoRepository.SalvarCarrinho(carrinho);

                var count = carrinho.Itens.Sum(i => i.Quantidade);
                var total = carrinho.Total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

                if (isAjax) return Json(new { ok = true, count, total, message = "Quantidade atualizada." });

                TempData["Sucesso"] ??= "Quantidade atualizada.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                if (isAjax) return Json(new { ok = false, error = $"Erro ao atualizar item: {ex.Message}" });
                TempData["Erro"] = $"Erro ao atualizar item: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoverItem(int produtoId, string? sabor)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
                             StringComparison.OrdinalIgnoreCase);

            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var chaveSabor = sabor ?? string.Empty;

                var item = carrinho.Itens.FirstOrDefault(i =>
                    i.Produto.Id == produtoId &&
                    (i.Sabor ?? string.Empty) == chaveSabor);

                if (item == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Item não encontrado no carrinho." });
                    TempData["Erro"] = "Item não encontrado no carrinho.";
                    return RedirectToAction(nameof(Index));
                }

                carrinho.Itens.Remove(item);
                _carrinhoRepository.SalvarCarrinho(carrinho);

                var count = carrinho.Itens.Sum(i => i.Quantidade);
                var total = carrinho.Total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
                var nomeProduto = item.Produto.Nome;
                var isEmpty = !carrinho.Itens.Any();

                if (isAjax) return Json(new { ok = true, count, total, isEmpty, message = $"{nomeProduto} removido do carrinho." });

                TempData["Sucesso"] = $"{nomeProduto} removido do carrinho.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                if (isAjax) return Json(new { ok = false, error = $"Erro ao remover item: {ex.Message}" });
                TempData["Erro"] = $"Erro ao remover item: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public IActionResult GetTotal()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            return Json(new { total = carrinho.Total.ToString("C", CultureInfo.GetCultureInfo("pt-BR")) });
        }

        // =================== Finalização ===================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finalizar(ResumoPedidoViewModel model)
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            if (carrinho == null || !carrinho.Itens.Any())
            {
                TempData["Erro"] = "Seu carrinho está vazio.";
                return RedirectToAction("Index");
            }

            var telefone = HttpContext.Session.GetString("ClienteTelefone");
            if (string.IsNullOrEmpty(telefone))
            {
                TempData["Erro"] = "Sessão expirada. Faça login novamente.";
                return RedirectToAction("Login", "Auth");
            }

            var cliente = _clienteRepository.ObterPorTelefone(telefone);
            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToAction("Login", "Auth");
            }

            // Cinturão 18+ na finalização
            if (CarrinhoRequerMaioridade(carrinho))
            {
                if (!cliente.DataNascimento.HasValue || Idade(cliente.DataNascimento.Value) < 18)
                {
                    TempData["Erro"] = "Não foi possível finalizar: itens exigem 18+ e o cadastro não atende a essa exigência.";
                    return RedirectToAction("Resumo");
                }
            }

            // Revalidação de estoque item-a-item (pode ajustar quantidades ou remover)
            bool houveAjuste = false;
            foreach (var i in carrinho.Itens.ToList())
            {
                var produto = _produtoRepository.ObterPorId(i.Produto.Id);
                if (produto == null)
                {
                    carrinho.Itens.Remove(i);
                    houveAjuste = true;
                    continue;
                }
                produto.DeserializarSaboresQuantidades();
                var disponivel = ObterEstoqueDisponivel(produto, i.Sabor ?? string.Empty);

                if (disponivel <= 0)
                {
                    carrinho.Itens.Remove(i);
                    houveAjuste = true;
                    continue;
                }
                if (i.Quantidade > disponivel)
                {
                    i.Quantidade = disponivel;
                    houveAjuste = true;
                }
            }
            _carrinhoRepository.SalvarCarrinho(carrinho);

            if (!carrinho.Itens.Any())
            {
                TempData["Erro"] = "Seu carrinho ficou sem estoque disponível para todos os itens.";
                return RedirectToAction(nameof(Index));
            }
            if (houveAjuste)
            {
                TempData["Erro"] = "Alguns itens foram ajustados conforme disponibilidade de estoque. Revise seu carrinho.";
                return RedirectToAction(nameof(Resumo));
            }

            var metodo = MapearMetodoPagamento(model.MetodoPagamento);
            if (metodo == null)
            {
                TempData["Erro"] = "Selecione um método de pagamento válido.";
                return RedirectToAction("Resumo");
            }

            EnderecoModel? enderecoSelecionado = null;
            if (!model.RetiradaNoLocal)
            {
                enderecoSelecionado = _clienteRepository.ObterEnderecos(cliente.Id)
                    ?.FirstOrDefault(e => e.Id == model.EnderecoSelecionadoId);

                if (enderecoSelecionado == null)
                {
                    TempData["Erro"] = "Selecione um endereço válido.";
                    return RedirectToAction("Resumo");
                }
            }

            var pedido = new PedidoModel
            {
                ClienteId = cliente.Id,
                EnderecoId = model.RetiradaNoLocal ? (int?)null : enderecoSelecionado!.Id,
                DataPedido = DateTime.Now,
                Status = "Pendente",
                ValorTotal = carrinho.Total,
                TaxaEntrega = model.RetiradaNoLocal ? 0m : CalcularTaxaEntrega(enderecoSelecionado!),
                MetodoPagamento = model.MetodoPagamento!,
                Observacoes = model.Observacoes,
                RetiradaNoLocal = model.RetiradaNoLocal,
                CodigoTransacao = Guid.NewGuid().ToString(),
                PedidoItens = carrinho.Itens.Select(i => new PedidoItemModel
                {
                    ProdutoId = i.Produto.Id,
                    Quantidade = i.Quantidade,
                    PrecoOriginal = i.Produto.Preco,
                    PrecoUnitario = i.Produto.EstaEmPromocao() && i.Produto.PrecoPromocional.HasValue
                        ? i.Produto.PrecoPromocional.Value
                        : i.Produto.Preco,
                    Observacoes = i.Observacoes,
                    Sabor = i.Sabor,
                    EstoqueBaixado = false,
                    EstoqueBaixadoEm = null
                }).ToList()
            };

            if (model.RetiradaNoLocal)
            {
                var loja = _lojaRepo.ObterDoLojistaAtual();
                if (loja != null)
                {
                    pedido.LojaNome = loja.Nome;
                    pedido.LojaEnderecoTexto = loja.EnderecoTexto;
                    pedido.LojaMapsUrl = loja.MapsUrl;
                }
            }

            bool pagaNaEntrega = (metodo == PaymentMethod.Cash) || (metodo == PaymentMethod.CardDebit);

            if (pagaNaEntrega)
            {
                // Pagamento confirmado depois (manual)
                pedido.Status = "Aguardando Pagamento (Entrega)";
                _pedidoRepository.Adicionar(pedido);

                // Broadcast para lojistas
                await _hub.Clients.Group("lojistas").SendAsync("NewOrder", new
                {
                    id = pedido.Id,
                    status = pedido.Status,
                    total = pedido.ValorTotal,
                    metodo = pedido.MetodoPagamento,
                    data = pedido.DataPedido,
                    cliente = new { id = cliente.Id, nome = cliente.Nome }
                });

                SetLastOrderCookie(pedido.RastreioToken);
                _carrinhoRepository.LimparCarrinho();
                HttpContext.Session.Remove("ClienteConfirmado");

                TempData["Sucesso"] = "Pedido realizado com sucesso! Pagamento na entrega.";
                return RedirectToAction("Confirmacao", new { id = pedido.Id });
            }
            else
            {
                // Fluxo online (PIX/Cartão). Notificação de “Pago” será emitida quando o status mudar.
                pedido.Status = "Pendente";
                _pedidoRepository.Adicionar(pedido);

                // Broadcast para lojistas (pedido criado)
                await _hub.Clients.Group("lojistas").SendAsync("NewOrder", new
                {
                    id = pedido.Id,
                    status = pedido.Status,
                    total = pedido.ValorTotal,
                    metodo = pedido.MetodoPagamento,
                    data = pedido.DataPedido,
                    cliente = new { id = cliente.Id, nome = cliente.Nome }
                });

                SetLastOrderCookie(pedido.RastreioToken);
                HttpContext.Session.Remove("ClienteConfirmado");

                return RedirectToAction("Checkout", "Pagamento", new { pedidoId = pedido.Id });
            }
        }

        // =================== Endereço ===================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdicionarEndereco(int ClienteId, [Bind(Prefix = "EnderecoNovo")] EnderecoModel endereco)
        {
            try
            {
                endereco.ClienteId = ClienteId;
                endereco.Estado = (endereco.Estado ?? "").Trim().ToUpper();

                // Formatar CEP 00000-000
                var dig = new string((endereco.CEP ?? "").Where(char.IsDigit).ToArray());
                if (dig.Length == 8)
                    endereco.CEP = $"{dig[..5]}-{dig[5..]}";

                // Validar DataAnnotations
                var validationContext = new ValidationContext(endereco, null, null);
                var validationResults = new List<ValidationResult>();
                bool isValid = Validator.TryValidateObject(endereco, validationContext, validationResults, true);

                if (!isValid)
                {
                    TempData["Erro"] = "Verifique os campos do novo endereço: " +
                                      string.Join(", ", validationResults.Select(v => v.ErrorMessage));
                    return RedirectToAction(nameof(Resumo));
                }

                var cliente = _clienteRepository.ObterPorId(ClienteId);
                if (cliente == null)
                {
                    TempData["Erro"] = "Cliente não encontrado.";
                    return RedirectToAction(nameof(Resumo));
                }

                // Verificar duplicidade básica
                var enderecosExistentes = _clienteRepository.ObterEnderecos(ClienteId) ?? new List<EnderecoModel>();
                var jaExiste = enderecosExistentes.Any(e =>
                    string.Equals((e.CEP ?? ""), endereco.CEP ?? "", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((e.Logradouro ?? "").Trim(), (endereco.Logradouro ?? "").Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((e.Numero ?? "").Trim(), (endereco.Numero ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                );

                if (jaExiste)
                {
                    TempData["Erro"] = "Este endereço já está cadastrado para o cliente.";
                    return RedirectToAction(nameof(Resumo));
                }

                // Definir como principal se for o primeiro endereço OU se foi marcado como principal
                endereco.Principal = !enderecosExistentes.Any() || endereco.Principal;

                // Garantir apenas um principal
                if (endereco.Principal && enderecosExistentes.Any(e => e.Principal))
                {
                    foreach (var outroEndereco in enderecosExistentes.Where(e => e.Principal))
                    {
                        outroEndereco.Principal = false;
                        _clienteRepository.AtualizarEndereco(outroEndereco);
                    }
                }

                var salvo = _clienteRepository.AdicionarEndereco(ClienteId, endereco);

                TempData["EnderecoNovoId"] = salvo?.Id;
                TempData["SkipConfirm"] = "1";
                TempData["Sucesso"] = "Endereço adicionado com sucesso!";
                return RedirectToAction(nameof(Resumo));
            }
            catch (Exception ex)
            {

                TempData["Erro"] = $"Não foi possível atualizar o endereço: {ex.Message}";
                return RedirectToAction(nameof(Resumo));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExcluirEndereco(int ClienteId, int id)
        {
            try
            {
                var novoId = _clienteRepository.RemoverEndereco(ClienteId, id);

                if (novoId.HasValue)
                    TempData["EnderecoNovoId"] = novoId.Value; // pré-selecionar no GET

                TempData["SkipConfirm"] = "1";
                TempData["Sucesso"] = "Endereço excluído com sucesso!";
            }
            catch (InvalidOperationException ex)
            {
                // mensagem amigável quando há pedidos vinculando o endereço
                TempData["Erro"] = ex.Message;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 547)
            {
                // fallback: se cair direto na violação de FK
                TempData["Erro"] = "Este endereço está vinculado a pedidos e não pode ser excluído.";
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Não foi possível excluir o endereço: {ex.Message}";
            }

            return RedirectToAction(nameof(Resumo));
        }

    }
}
