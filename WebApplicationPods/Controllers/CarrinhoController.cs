using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class CarrinhoController : Controller
    {
        private readonly ICarrinhoRepository _carrinhoRepository;
        private readonly IProdutoRepository _produtoRepository;
        private readonly IClienteRepository _clienteRepository;
        private readonly IPedidoRepository _pedidoRepository;

        public CarrinhoController(
            ICarrinhoRepository carrinhoRepository,
            IProdutoRepository produtoRepository,
            IClienteRepository clienteRepository,
            IPedidoRepository pedidoRepository)
        {
            _carrinhoRepository = carrinhoRepository;
            _produtoRepository = produtoRepository;
            _clienteRepository = clienteRepository;
            _pedidoRepository = pedidoRepository;
        }

        // =================== Helpers ===================

        // Grava o token de rastreio do último pedido em cookie (30 dias)
        private void SetLastOrderCookie(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            var opts = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = false,                 // true se não precisar ler via JS
                Secure = Request.IsHttps,       // em produção com HTTPS = true
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            };
            Response.Cookies.Append("last_order_token", token!, opts);
        }

        private int ObterEstoqueDisponivel(ProdutoModel produto, string sabor)
        {
            if (produto.SaboresQuantidadesList?.Any() == true && !string.IsNullOrWhiteSpace(sabor))
            {
                var sq = produto.SaboresQuantidadesList
                    .FirstOrDefault(s => s.Sabor.Equals(sabor, StringComparison.OrdinalIgnoreCase));
                return sq?.Quantidade ?? 0;
            }
            return produto.Estoque;
        }

        // Atualização: quantidade ABSOLUTA não pode exceder o disponível
        private bool ValidarEstoqueAtualizacao(ProdutoModel produto, int quantidade, string sabor, out string mensagemErro)
        {
            mensagemErro = string.Empty;

            var disponivel = ObterEstoqueDisponivel(produto, sabor);

            if (quantidade > disponivel)
            {
                mensagemErro = string.IsNullOrWhiteSpace(sabor)
                    ? $"Estoque insuficiente. Disponível: {disponivel}"
                    : $"Estoque insuficiente para o sabor {sabor}. Disponível: {disponivel}";
                return false;
            }

            return true;
        }

        // Adição: quantidade no carrinho + novaQtd não pode exceder o disponível
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

        private PaymentMethod? MapearMetodoPagamento(string metodo)
        {
            if (string.IsNullOrWhiteSpace(metodo)) return null;

            metodo = metodo.Trim().ToLowerInvariant();

            return metodo switch
            {
                "dinheiro" => PaymentMethod.Cash,
                "pix" => PaymentMethod.Pix,
                "cartão de crédito" => PaymentMethod.CardCredit,
                "cartao de credito" => PaymentMethod.CardCredit,
                "cartão de débito" => PaymentMethod.CardDebit,
                "cartao de debito" => PaymentMethod.CardDebit,
                _ => (PaymentMethod?)null
            };
        }

        private decimal CalcularTaxaEntrega(EnderecoModel endereco)
        {
            return 5m; // ajuste sua regra aqui
        }

        // =================== Actions ===================

        public IActionResult Index()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            return View(carrinho);
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
                TempData["Erro"] = "Seu carrinho está vazio";
                return RedirectToAction("Index");
            }

            var telefone = HttpContext.Session.GetString("ClienteTelefone");
            if (string.IsNullOrEmpty(telefone))
            {
                TempData["ReturnUrl"] = Url.Action("Resumo", "Carrinho");
                return RedirectToAction("Login", "Auth");
            }

            var cliente = _clienteRepository.ObterPorTelefone(telefone);
            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado";
                return RedirectToAction("Login", "Auth");
            }

            var enderecos = _clienteRepository.ObterEnderecos(cliente.Id)?.ToList() ?? new List<EnderecoModel>();

            int? novoId = null;
            if (TempData.ContainsKey("EnderecoNovoId") && int.TryParse(Convert.ToString(TempData["EnderecoNovoId"]), out var parsed))
                novoId = parsed;

            var enderecoSelecionado = (novoId.HasValue ? enderecos.FirstOrDefault(e => e.Id == novoId.Value) : null)
                                      ?? enderecos.FirstOrDefault(e => e.Principal);

            var viewModel = new ResumoPedidoViewModel
            {
                Carrinho = carrinho,
                Cliente = cliente,
                EnderecosDisponiveis = enderecos,
                EnderecoSelecionadoId = enderecoSelecionado?.Id ?? 0,
                EnderecoEntrega = enderecoSelecionado,
                Observacoes = "",
                RetiradaNoLocal = false
            };

            return View(viewModel);
        }

        public IActionResult Confirmacao(int id)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
            {
                TempData["Erro"] = "Pedido não encontrado!";
                return RedirectToAction("Index");
            }

            ViewBag.PedidoId = pedido.Id; // ex.: usar no layout/botões
            return View(pedido);
        }


        [HttpPost]
        [ValidateAntiForgeryToken] // opcional, mas recomendado
        public IActionResult AdicionarItem(
    int produtoId,
    int quantidade,
    string? sabor = null,
    string? observacoes = null,
    bool buyNow = false)
        {
            bool isAjax = string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            try
            {
                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Produto não encontrado." });
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction("Index");
                }

                // estoque por sabor (se aplicável)
                produto.DeserializarSaboresQuantidades();

                // valida estoque: existente + nova quantidade não pode exceder o disponível
                if (!ValidarEstoqueAoAdicionar(produto, quantidade, sabor ?? string.Empty, out var mensagemErro))
                {
                    if (isAjax) return Json(new { ok = false, error = mensagemErro });
                    TempData["Erro"] = mensagemErro;
                    return RedirectToAction("Detalhes", "Produto", new { id = produtoId });
                }

                _carrinhoRepository.AdicionarItem(produto, quantidade, sabor, observacoes);

                // total de itens para atualizar o badge
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var count = carrinho?.Itens?.Sum(i => i.Quantidade) ?? 0;

                if (isAjax)
                {
                    // IMPORTANTE: devolve também o nome para o toast do front
                    return Json(new { ok = true, count, nome = produto.Nome });
                }

                TempData["Sucesso"] = $"{produto.Nome} adicionado ao carrinho!";
                return buyNow ? RedirectToAction("Resumo") : RedirectToAction("Index");
            }
            catch (Exception)
            {
                if (isAjax) return Json(new { ok = false, error = "Erro ao adicionar ao carrinho." });
                TempData["Erro"] = "Ocorreu um erro ao adicionar o produto ao carrinho.";
                return RedirectToAction("Index");
            }
        }


        [HttpGet]
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
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var chaveSabor = sabor ?? string.Empty;

                var item = carrinho.Itens.FirstOrDefault(i =>
                    i.Produto.Id == produtoId &&
                    (i.Sabor ?? string.Empty) == chaveSabor);

                if (item == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Item não encontrado no carrinho!" });
                    TempData["Erro"] = "Item não encontrado no carrinho!";
                    return RedirectToAction(nameof(Index));
                }

                // Determina a nova quantidade
                var novaQtd = item.Quantidade;
                if (string.Equals(op, "inc", StringComparison.OrdinalIgnoreCase))
                    novaQtd = item.Quantidade + 1;
                else if (string.Equals(op, "dec", StringComparison.OrdinalIgnoreCase))
                    novaQtd = item.Quantidade - 1;
                else if (quantidade.HasValue)
                    novaQtd = quantidade.Value;

                // Carrega produto e calcula estoque máximo (por sabor ou geral)
                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Produto não encontrado!" });
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction(nameof(Index));
                }

                produto.DeserializarSaboresQuantidades();

                var maxPermitido = produto.Estoque;
                if (!string.IsNullOrWhiteSpace(chaveSabor) &&
                    produto.SaboresQuantidadesList?.Any() == true)
                {
                    var sq = produto.SaboresQuantidadesList
                        .FirstOrDefault(s => string.Equals(s.Sabor, chaveSabor, StringComparison.OrdinalIgnoreCase));
                    maxPermitido = sq?.Quantidade ?? produto.Estoque;
                }
                if (maxPermitido < 1) maxPermitido = 1;

                // Clamp no servidor
                if (novaQtd < 1) novaQtd = 1;

                if (novaQtd > maxPermitido)
                {
                    novaQtd = maxPermitido;
                    if (isAjax) return Json(new { ok = false, error = $"Quantidade ajustada ao máximo disponível ({maxPermitido})." });
                    TempData["Erro"] = $"Quantidade ajustada ao máximo disponível ({maxPermitido}).";
                }

                item.Quantidade = novaQtd;
                _carrinhoRepository.SalvarCarrinho(carrinho);

                // Total de itens no carrinho (para o badge)
                var count = carrinho.Itens.Sum(i => i.Quantidade);
                // Total monetário do carrinho (para o card de resumo)
                var total = carrinho.Total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

                if (isAjax)
                {
                    return Json(new
                    {
                        ok = true,
                        count,
                        total,
                        message = "Quantidade atualizada."
                    });
                }

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
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var chaveSabor = sabor ?? string.Empty;

                var item = carrinho.Itens.FirstOrDefault(i =>
                    i.Produto.Id == produtoId &&
                    (i.Sabor ?? string.Empty) == chaveSabor);

                if (item == null)
                {
                    if (isAjax) return Json(new { ok = false, error = "Item não encontrado no carrinho!" });
                    TempData["Erro"] = "Item não encontrado no carrinho!";
                    return RedirectToAction(nameof(Index));
                }

                carrinho.Itens.Remove(item);
                _carrinhoRepository.SalvarCarrinho(carrinho);

                var count = carrinho.Itens.Sum(i => i.Quantidade);
                var total = carrinho.Total.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
                var nomeProduto = item.Produto.Nome;
                var isEmpty = !carrinho.Itens.Any();

                if (isAjax)
                {
                    return Json(new
                    {
                        ok = true,
                        count,
                        total,
                        isEmpty,
                        message = $"{nomeProduto} removido do carrinho."
                    });
                }

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
        public IActionResult Finalizar(ResumoPedidoViewModel model)
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            if (carrinho == null || !carrinho.Itens.Any())
            {
                TempData["Erro"] = "Seu carrinho está vazio";
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

            var metodo = MapearMetodoPagamento(model.MetodoPagamento);
            if (metodo == null)
            {
                TempData["Erro"] = "Selecione um método de pagamento válido.";
                return RedirectToAction("Resumo");
            }

            EnderecoModel enderecoSelecionado = null;
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

            var taxaEntrega = model.RetiradaNoLocal ? 0m : CalcularTaxaEntrega(enderecoSelecionado);

            var pedido = new PedidoModel
            {
                ClienteId = cliente.Id,
                EnderecoId = model.RetiradaNoLocal ? 0 : (enderecoSelecionado?.Id ?? 0),
                DataPedido = DateTime.Now,
                Status = "Pendente",
                ValorTotal = carrinho.Total,
                TaxaEntrega = taxaEntrega,
                MetodoPagamento = model.MetodoPagamento,
                CodigoTransacao = Guid.NewGuid().ToString(),
                PedidoItens = carrinho.Itens.Select(i => new PedidoItemModel
                {
                    ProdutoId = i.Produto.Id,
                    Quantidade = i.Quantidade,
                    PrecoUnitario = i.Produto.Preco,
                    Observacoes = model.Observacoes
                }).ToList()
            };

            bool pagaNaEntrega = (metodo == PaymentMethod.Cash) || (metodo == PaymentMethod.CardDebit);

            if (pagaNaEntrega)
            {
                pedido.Status = "Aguardando Pagamento (Entrega)";

                // O repositório deve GERAR pedido.RastreioToken antes de salvar
                _pedidoRepository.Adicionar(pedido);

                // Grava o cookie com o token de rastreio
                SetLastOrderCookie(pedido.RastreioToken);

                _carrinhoRepository.LimparCarrinho();
                TempData["Sucesso"] = "Pedido realizado com sucesso! Pagamento na entrega.";

                // Você pode redirecionar direto ao acompanhamento também:
                // return RedirectToAction("Acompanhar", "Pedido", new { id = pedido.Id, t = pedido.RastreioToken });

                return RedirectToAction("Confirmacao", new { id = pedido.Id });
            }
            else
            {
                pedido.Status = "Pendente";

                _pedidoRepository.Adicionar(pedido);

                // Grava o cookie com o token de rastreio
                SetLastOrderCookie(pedido.RastreioToken);

                return RedirectToAction("Checkout", "Pagamento", new { pedidoId = pedido.Id });
            }
        }

        // =================== Endereço ===================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AtualizarEndereco(int ClienteId, EnderecoModel endereco)
        {
            endereco.ClienteId = ClienteId;
            endereco.Estado = (endereco.Estado ?? "").Trim().ToUpper();
            var apenasNum = new string((endereco.CEP ?? "").Where(char.IsDigit).ToArray());
            if (apenasNum.Length == 8)
                endereco.CEP = $"{apenasNum[..5]}-{apenasNum[5..]}";

            ModelState.Remove(nameof(endereco.Cliente));
            ModelState.Remove(nameof(endereco.ClienteId));
            ModelState.Clear();

            if (!TryValidateModel(endereco, prefix: ""))
            {
                TempData["Erro"] = "Verifique os campos do endereço.";
                return RedirectToAction(nameof(Resumo));
            }

            var cliente = _clienteRepository.ObterPorId(ClienteId);
            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToAction(nameof(Resumo));
            }

            try
            {
                var atualizado = _clienteRepository.AtualizarEndereco(endereco);

                if (endereco.Principal)
                    _clienteRepository.DefinirEnderecoPrincipal(ClienteId, endereco.Id);

                TempData["EnderecoNovoId"] = endereco.Id;
                TempData["Sucesso"] = "Endereço atualizado com sucesso!";
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
        public IActionResult AdicionarEndereco(int ClienteId, [Bind(Prefix = "EnderecoNovo")] EnderecoModel endereco)
        {
            endereco.ClienteId = ClienteId;
            endereco.Estado = (endereco.Estado ?? "").Trim().ToUpper();
            var dig = new string((endereco.CEP ?? "").Where(char.IsDigit).ToArray());
            if (dig.Length == 8) endereco.CEP = $"{dig[..5]}-{dig[5..]}";

            ModelState.Remove(nameof(endereco.Cliente));
            ModelState.Remove(nameof(endereco.ClienteId));
            ModelState.Clear();

            if (!TryValidateModel(endereco, prefix: ""))
            {
                TempData["Erro"] = "Verifique os campos do novo endereço.";
                return RedirectToAction(nameof(Resumo));
            }

            var cliente = _clienteRepository.ObterPorId(ClienteId);
            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToAction(nameof(Resumo));
            }

            try
            {
                var jaExiste = _clienteRepository.ObterEnderecos(ClienteId)?.Any(e =>
                    string.Equals((e.CEP ?? ""), endereco.CEP ?? "", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((e.Logradouro ?? "").Trim(), (endereco.Logradouro ?? "").Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((e.Numero ?? "").Trim(), (endereco.Numero ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                ) == true;

                if (jaExiste)
                {
                    TempData["Erro"] = "Este endereço já está cadastrado para o cliente.";
                    return RedirectToAction(nameof(Resumo));
                }

                var possuiEnderecos = _clienteRepository.ObterEnderecos(ClienteId)?.Any() == true;
                endereco.Principal = !possuiEnderecos;

                var salvo = _clienteRepository.AdicionarEndereco(ClienteId, endereco);

                if (salvo != null && salvo.Principal)
                    _clienteRepository.DefinirEnderecoPrincipal(ClienteId, salvo.Id);

                TempData["EnderecoNovoId"] = salvo?.Id;
                TempData["Sucesso"] = "Endereço adicionado com sucesso!";
                return RedirectToAction(nameof(Resumo));
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Não foi possível salvar o endereço: {ex.Message}";
                return RedirectToAction(nameof(Resumo));
            }
        }
    }
}
