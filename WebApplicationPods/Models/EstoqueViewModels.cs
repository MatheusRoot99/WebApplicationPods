using System.ComponentModel.DataAnnotations;
using WebApplicationPods.Enum;

namespace WebApplicationPods.Models;

public class EstoqueFiltroVM
{
    public string? Categoria { get; set; }

    [Display(Name = "Apenas baixo estoque")]
    public bool ApenasBaixoEstoque { get; set; }

    [Display(Name = "Apenas esgotados")]
    public bool ApenasEsgotados { get; set; }

    [Display(Name = "Lançado de")]
    public DateTime? LancamentoDe { get; set; }

    [Display(Name = "até")]
    public DateTime? LancamentoAte { get; set; }

    [Display(Name = "Ordenar por")]
    public string? OrdenarPor { get; set; } = "nome_az";

    [Display(Name = "Limite baixo estoque")]
    public int LimiteBaixoEstoque { get; set; } = 5;

    public IEnumerable<string> CategoriasDisponiveis { get; set; } = Enumerable.Empty<string>();

    public IDictionary<string, string> OpcoesOrdenacao { get; set; } = new Dictionary<string, string>
    {
        ["nome_az"] = "Nome (A-Z)",
        ["nome_za"] = "Nome (Z-A)",
        ["estoque_ma"] = "Estoque (maior-menor)",
        ["estoque_me"] = "Estoque (menor-maior)",
        ["valor_ma"] = "Valor em estoque (maior-menor)",
        ["valor_me"] = "Valor em estoque (menor-maior)",
        ["data_new"] = "Mais novos",
        ["data_old"] = "Mais antigos"
    };
}

public class EstoqueItemVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Categoria { get; set; }
    public int Estoque { get; set; }
    public ProdutoTipo TipoProduto { get; set; }
    public BebidaEmbalagemTipo? BebidaEmbalagem { get; set; }
    public int? BebidaQtdPorEmbalagem { get; set; }

    public decimal Preco { get; set; }
    public decimal? PrecoPromocional { get; set; }
    public bool EmPromocao { get; set; }
    public string? ImagemUrl { get; set; }

    public DateTime? Lancamento { get; set; }

    public decimal ValorVendaEmEstoque =>
        (EmPromocao && PrecoPromocional.HasValue ? PrecoPromocional.Value : Preco) * Estoque;

    public bool EhEmbalagemComposta =>
        TipoProduto == ProdutoTipo.BebidaAlcoolica
        && BebidaQtdPorEmbalagem.HasValue
        && BebidaQtdPorEmbalagem.Value > 1
        && BebidaEmbalagem is BebidaEmbalagemTipo.Pack
            or BebidaEmbalagemTipo.Fardo
            or BebidaEmbalagemTipo.Caixa;

    public int UnidadesFisicasPorEmbalagem => EhEmbalagemComposta
        ? BebidaQtdPorEmbalagem!.Value
        : 1;

    public int EstoqueFisicoTotal => Math.Max(0, Estoque) * UnidadesFisicasPorEmbalagem;

    public string EstoqueDescricao
    {
        get
        {
            var quantidade = Math.Max(0, Estoque);
            if (!EhEmbalagemComposta)
                return quantidade == 1 ? "1 unidade" : $"{quantidade} unidades";

            var singular = BebidaEmbalagem == BebidaEmbalagemTipo.Fardo
                ? "fardo"
                : BebidaEmbalagem == BebidaEmbalagemTipo.Caixa
                    ? "caixa"
                    : "pack";
            var plural = singular == "caixa" ? "caixas" : $"{singular}s";
            var embalagem = quantidade == 1 ? singular : plural;
            var unidadeFisica = EstoqueFisicoTotal == 1 ? "unidade fisica" : "unidades fisicas";
            return $"{quantidade} {embalagem} ({EstoqueFisicoTotal} {unidadeFisica})";
        }
    }

    public bool Esgotado => Estoque <= 0;
    public bool BaixoEstoque(int limite) => !Esgotado && Estoque <= Math.Max(1, limite);
}

public class EstoqueVM
{
    public EstoqueFiltroVM Filtros { get; set; } = new();
    public IList<EstoqueItemVM> Itens { get; set; } = new List<EstoqueItemVM>();

    public int TotalSkus => Itens.Count;
    public int QtdTotalEstoque => Itens.Sum(i => i.Estoque);
    public decimal ValorVendaTotal => Itens.Sum(i => i.ValorVendaEmEstoque);
    public int BaixoEstoqueCount => Itens.Count(i => i.BaixoEstoque(Filtros.LimiteBaixoEstoque));
    public int EsgotadosCount => Itens.Count(i => i.Esgotado);
    public decimal? ValorCustoTotal => null;
}
