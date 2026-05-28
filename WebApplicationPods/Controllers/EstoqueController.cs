using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista,Admin")]
    public class EstoqueController : Controller
    {
        private readonly IProdutoRepository _produtos;

        public EstoqueController(IProdutoRepository produtos)
        {
            _produtos = produtos;
        }

        private IQueryable<ProdutoModel> QueryProdutos()
            => (_produtos.Query() ?? throw new NotImplementedException("Implemente IProdutoRepository.Query()"))
               .AsNoTracking();

        [HttpGet]
        public IActionResult Index(EstoqueFiltroVM filtros)
        {
            filtros ??= new EstoqueFiltroVM();

            var q = QueryProdutos();

            var categorias = q
                .Where(p => p.Categoria != null && p.Categoria.Nome != null)
                .Select(p => p.Categoria!.Nome)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
                q = q.Where(p => p.Categoria != null && p.Categoria.Nome == filtros.Categoria);

            if (filtros.ApenasEsgotados)
                q = q.Where(p => p.Estoque <= 0);

            if (filtros.ApenasBaixoEstoque)
                q = q.Where(p => p.Estoque > 0 && p.Estoque <= filtros.LimiteBaixoEstoque);

            if (filtros.LancamentoDe.HasValue)
                q = q.Where(p => p.DataCadastro >= filtros.LancamentoDe.Value);

            if (filtros.LancamentoAte.HasValue)
            {
                var ate = filtros.LancamentoAte.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(p => p.DataCadastro <= ate);
            }

            var itens = q.Select(p => new EstoqueItemVM
            {
                Id = p.Id,
                Nome = p.Nome,
                Categoria = p.Categoria != null ? p.Categoria.Nome : "-",
                Estoque = p.Estoque,
                TipoProduto = p.TipoProduto,
                BebidaEmbalagem = p.BebidaEmbalagem,
                BebidaQtdPorEmbalagem = p.BebidaQtdPorEmbalagem,
                Preco = p.Preco,
                PrecoPromocional = p.PrecoPromocional,
                EmPromocao = p.EmPromocao,
                Lancamento = p.DataCadastro,
                ImagemUrl = p.ImagemUrl
            });

            itens = filtros.OrdenarPor switch
            {
                "nome_za" => itens.OrderByDescending(i => i.Nome),
                "estoque_ma" => itens.OrderByDescending(i => i.Estoque),
                "estoque_me" => itens.OrderBy(i => i.Estoque),
                "valor_ma" => itens.OrderByDescending(i => i.ValorVendaEmEstoque),
                "valor_me" => itens.OrderBy(i => i.ValorVendaEmEstoque),
                "data_new" => itens.OrderByDescending(i => i.Lancamento),
                "data_old" => itens.OrderBy(i => i.Lancamento),
                _ => itens.OrderBy(i => i.Nome)
            };

            var vm = new EstoqueVM
            {
                Filtros = filtros,
                Itens = itens.ToList()
            };

            vm.Filtros.CategoriasDisponiveis = categorias;
            vm.Filtros.OpcoesOrdenacao = new EstoqueFiltroVM().OpcoesOrdenacao;

            return View(vm);
        }

        [HttpGet]
        public IActionResult ExportarCsv(EstoqueFiltroVM filtros)
        {
            var result = (Index(filtros) as ViewResult)?.Model as EstoqueVM;
            if (result == null) return NotFound();

            var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            var lines = new List<string>
            {
                $"Id{sep}Nome{sep}Categoria{sep}Estoque{sep}Preço{sep}Promoção{sep}PreçoPromo{sep}ValorVendaEstoque{sep}Lançamento"
            };

            foreach (var i in result.Itens)
            {
                lines.Add(string.Join(sep, new[]
                {
                    i.Id.ToString(),
                    Csv(i.Nome),
                    Csv(i.Categoria),
                    i.Estoque.ToString(),
                    i.Preco.ToString("0.00"),
                    i.EmPromocao ? "Sim" : "Não",
                    i.PrecoPromocional?.ToString("0.00") ?? "",
                    i.ValorVendaEmEstoque.ToString("0.00"),
                    i.Lancamento?.ToString("yyyy-MM-dd") ?? ""
                }));
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
            return File(bytes, "text/csv; charset=utf-8", $"estoque_{DateTime.Now:yyyyMMdd_HHmm}.csv");

            static string Csv(string? s) => string.IsNullOrEmpty(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
        }
    }
}
