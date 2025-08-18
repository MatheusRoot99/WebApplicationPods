using Microsoft.AspNetCore.Mvc;
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
        public CarrinhoController(ICarrinhoRepository carrinhoRepository, IProdutoRepository produtoRepository,
            IClienteRepository clienteRepository,
            IPedidoRepository pedidoRepository)
        {
            _carrinhoRepository = carrinhoRepository;
            _produtoRepository = produtoRepository;
            _clienteRepository = clienteRepository;
            _pedidoRepository = pedidoRepository;
        }

        public IActionResult Index()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            return View(carrinho);
        }

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

                // Verifica estoque disponível
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
        public IActionResult FinalizarPedido(ResumoPedidoViewModel model)
        {
            try
            {
                // 1. Obter cliente autenticado
                var telefone = HttpContext.Session.GetString("ClienteTelefone");
                if (string.IsNullOrEmpty(telefone))
                {
                    return RedirectToAction("Login", "Auth");
                }

                var cliente = _clienteRepository.ObterPorTelefone(telefone);
                if (cliente == null)
                {
                    return RedirectToAction("Login", "Auth");
                }

                // 2. Validar endereço selecionado
                if (model.EnderecoSelecionadoId == 0)
                {
                    ModelState.AddModelError("", "Selecione um endereço de entrega");
                    return View("Resumo", model);
                }

                // 3. Validar carrinho
                if (model.Carrinho == null || !model.Carrinho.Itens.Any())
                {
                    TempData["Erro"] = "Seu carrinho está vazio";
                    return RedirectToAction("Index");
                }

                // 4. Criar pedido
                var pedido = new PedidoModel
                {
                    ClienteId = cliente.Id,
                    EnderecoId = model.EnderecoSelecionadoId,
                    Status = "Aguardando Pagamento",
                    ValorTotal = model.Carrinho.Total,
                    MetodoPagamento = model.MetodoPagamento,
                    DataPedido = DateTime.Now,
                    PedidoItens = model.Carrinho.Itens.Select(item => new PedidoItemModel
                    {
                        ProdutoId = item.Produto.Id,
                        Quantidade = item.Quantidade,
                        PrecoUnitario = item.PrecoUnitario,
                        Observacoes = item.Observacoes
                        // Removido Sabor pois não existe no modelo
                    }).ToList()
                };

                // 5. Atualizar estoques (se necessário)
                foreach (var item in model.Carrinho.Itens)
                {
                    var produto = _produtoRepository.ObterPorId(item.Produto.Id);
                    if (produto != null)
                    {
                        // Removida a verificação de Sabor já que não está mais no fluxo
                        produto.Estoque -= item.Quantidade;
                        _produtoRepository.Atualizar(produto);
                    }
                }

                // 6. Salvar pedido
                _pedidoRepository.Adicionar(pedido);
                _carrinhoRepository.LimparCarrinho();

                // 7. Redirecionar para confirmação
                return RedirectToAction("Confirmacao", new { id = pedido.Id });
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao finalizar pedido: {ex.Message}";
                return RedirectToAction("Resumo");
            }
        }

        public IActionResult Resumo()
        {
            // 1. Obter carrinho
            var carrinho = _carrinhoRepository.ObterCarrinho();
            if (carrinho == null || !carrinho.Itens.Any())
            {
                TempData["Erro"] = "Seu carrinho está vazio";
                return RedirectToAction("Index");
            }

            // 2. Verificar autenticação
            var telefone = HttpContext.Session.GetString("ClienteTelefone");
            if (string.IsNullOrEmpty(telefone))
            {
                TempData["ReturnUrl"] = Url.Action("Resumo", "Carrinho");
                return RedirectToAction("Login", "Auth");
            }

            // 3. Obter cliente com endereços
            var cliente = _clienteRepository.ObterPorTelefone(telefone);
            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado";
                return RedirectToAction("Login", "Auth");
            }

            // 4. Obter endereços do cliente
            var enderecos = _clienteRepository.ObterEnderecos(cliente.Id) ?? new List<EnderecoModel>();

            // 5. Criar view model
            var viewModel = new ResumoPedidoViewModel
            {
                Carrinho = carrinho,
                Cliente = cliente,
                EnderecosDisponiveis = _clienteRepository.ObterEnderecos(cliente.Id)?.ToList() ?? new List<EnderecoModel>(),
                EnderecoSelecionadoId = enderecos.FirstOrDefault(e => e.Principal)?.Id ?? 0
            };

            return View(viewModel);
        }


        // 🔹 Método centralizado para validação de estoque
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
    }
}