using WebApplicationPods.Enum;
using WebApplicationPods.Models;

namespace WebApplicationPods.Tests;

public class ProdutoModelTests
{
    [Fact]
    public void Bebida_pack_mostra_estoque_vendavel_e_total_fisico()
    {
        var produto = new ProdutoModel
        {
            TipoProduto = ProdutoTipo.BebidaAlcoolica,
            BebidaEmbalagem = BebidaEmbalagemTipo.Pack,
            BebidaQtdPorEmbalagem = 6,
            Estoque = 6
        };

        Assert.True(produto.EhEmbalagemComposta);
        Assert.Equal(6, produto.UnidadesFisicasPorEmbalagem);
        Assert.Equal(36, produto.EstoqueFisicoTotal);
        Assert.Equal("pack com 6 unidades", produto.UnidadeVendaDescricao);
        Assert.Equal("6 packs (36 unidades físicas)", produto.EstoqueDescricao);
    }

    [Fact]
    public void Produto_padrao_continua_sendo_controlado_por_unidade()
    {
        var produto = new ProdutoModel
        {
            TipoProduto = ProdutoTipo.Padrao,
            Estoque = 2
        };

        Assert.False(produto.EhEmbalagemComposta);
        Assert.Equal(1, produto.UnidadesFisicasPorEmbalagem);
        Assert.Equal(2, produto.EstoqueFisicoTotal);
        Assert.Equal("2 unidades", produto.EstoqueDescricao);
    }
}
