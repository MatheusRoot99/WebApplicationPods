using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface; // ajuste se necessário

public class EstoqueController : Controller
{
    private readonly IProdutoRepository _produtos;

    public EstoqueController(IProdutoRepository produtos)
    {
        _produtos = produtos;
    }

    // Se o repositório não tiver Query(), pode usar ObterTodos().AsQueryable()
    private IQueryable<ProdutoModel> QueryProdutos()
        => (_produtos.Query() ?? throw new NotImplementedException("Implemente IProdutoRepository.Query()"))
           .AsNoTracking();

    [HttpGet]
    public IActionResult Index(EstoqueFiltroVM filtros)
    {
        var q = QueryProdutos();

        // Categorias disponíveis
        var categorias = q
            .Select(p => p.Categoria.Nome)
            .Where(n => n != null)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // Filtros
        if (!string.IsNullOrWhiteSpace(filtros.Categoria))
            q = q.Where(p => p.Categoria.Nome == filtros.Categoria);

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
            Categoria = p.Categoria.Nome,
            Estoque = p.Estoque,
            Preco = p.Preco,
            PrecoPromocional = p.PrecoPromocional,
            EmPromocao = p.EmPromocao,
            Lancamento = p.DataCadastro,
            ImagemUrl = p.ImagemUrl                 // <-- NOVO
        });

        // Ordenação
        itens = filtros.OrdenarPor switch
        {
            "nome_za" => itens.OrderByDescending(i => i.Nome),
            "estoque_ma" => itens.OrderByDescending(i => i.Estoque),
            "estoque_me" => itens.OrderBy(i => i.Estoque),
            "valor_ma" => itens.OrderByDescending(i => i.ValorVendaEmEstoque),
            "valor_me" => itens.OrderBy(i => i.ValorVendaEmEstoque),
            "data_new" => itens.OrderByDescending(i => i.Lancamento),
            "data_old" => itens.OrderBy(i => i.Lancamento),
            _ => itens.OrderBy(i => i.Nome),
        };

        var vm = new EstoqueVM
        {
            Filtros = filtros,
            Itens = itens.ToList()
        };

        // como EstoqueFiltroVM é class (não record), setamos propriedades “na mão”
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
