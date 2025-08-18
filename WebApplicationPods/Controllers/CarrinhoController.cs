using Microsoft.AspNetCore.Mvc;
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

        // Actions
        public IActionResult Index()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            return View(carrinho);
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

            return View(pedido);
        }

        // CRUD Actions
        [HttpPost]
        public IActionResult AdicionarItem(int produtoId, int quantidade, string sabor = null, string observacoes = null, bool buyNow = false)
        {
            try
            {
                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction("Index");
                }

                if (!ValidarEstoque(produto, quantidade, sabor, out string mensagemErro))
                {
                    TempData["Erro"] = mensagemErro;
                    return RedirectToAction("Detalhes", "Produto", new { id = produtoId });
                }

                _carrinhoRepository.AdicionarItem(produto, quantidade, sabor, observacoes);

                TempData["Sucesso"] = $"{produto.Nome} adicionado ao carrinho!";
                return buyNow ? RedirectToAction("Resumo") : RedirectToAction("Index");
            }
            catch
            {
                TempData["Erro"] = "Ocorreu um erro ao adicionar o produto ao carrinho.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult AtualizarItem(int produtoId, int quantidade, string sabor = null)
        {
            try
            {
                if (quantidade <= 0)
                {
                    TempData["Erro"] = "Quantidade deve ser maior que zero!";
                    return RedirectToAction("Index");
                }

                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction("Index");
                }

                if (!ValidarEstoque(produto, quantidade, sabor, out string mensagemErro))
                {
                    TempData["Erro"] = mensagemErro;
                    return RedirectToAction("Index");
                }

                var carrinho = _carrinhoRepository.ObterCarrinho();
                var item = carrinho.Itens.FirstOrDefault(i => i.Produto.Id == produtoId && i.Sabor == sabor);

                if (item != null)
                {
                    item.Quantidade = quantidade;
                    _carrinhoRepository.SalvarCarrinho(carrinho);
                    TempData["Sucesso"] = $"Quantidade de {produto.Nome} atualizada para {quantidade}.";
                }
                else
                {
                    TempData["Erro"] = "Item não encontrado no carrinho!";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao atualizar item: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult RemoverItem(int produtoId, string sabor = null)
        {
            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var item = carrinho.Itens.FirstOrDefault(i => i.Produto.Id == produtoId && i.Sabor == sabor);

                if (item != null)
                {
                    carrinho.Itens.Remove(item);
                    _carrinhoRepository.SalvarCarrinho(carrinho);
                    TempData["Sucesso"] = $"{item.Produto.Nome} removido do carrinho.";
                }
                else
                {
                    TempData["Erro"] = "Item não encontrado no carrinho!";
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao remover item: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

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
                EnderecoId = model.RetiradaNoLocal ? 0 : enderecoSelecionado.Id,
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
                _pedidoRepository.Adicionar(pedido);
                _carrinhoRepository.LimparCarrinho();
                TempData["Sucesso"] = "Pedido realizado com sucesso! Pagamento na entrega.";
                return RedirectToAction("Confirmacao", new { id = pedido.Id });
            }
            else
            {
                pedido.Status = "Pendente";
                _pedidoRepository.Adicionar(pedido);
                return RedirectToAction("Checkout", "Pagamento", new { pedidoId = pedido.Id });
            }
        }

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

        // Helper Methods
        private bool ValidarEstoque(ProdutoModel produto, int quantidade, string sabor, out string mensagemErro)
        {
            mensagemErro = string.Empty;

            if (produto.SaboresQuantidadesList?.Any() == true && !string.IsNullOrEmpty(sabor))
            {
                var saborSelecionado = produto.SaboresQuantidadesList.FirstOrDefault(s => s.Sabor == sabor);
                if (saborSelecionado == null)
                {
                    mensagemErro = "Sabor selecionado inválido.";
                    return false;
                }

                if (saborSelecionado.Quantidade < quantidade)
                {
                    mensagemErro = $"Estoque insuficiente para o sabor {sabor}. Disponível: {saborSelecionado.Quantidade}";
                    return false;
                }
            }
            else if (produto.Estoque < quantidade)
            {
                mensagemErro = $"Estoque insuficiente. Disponível: {produto.Estoque}";
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
                _ => null
            };
        }

        private decimal CalcularTaxaEntrega(EnderecoModel endereco)
        {
            return 5m;
        }
    }
}