using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebApplicationPods.Models
{
    public class ResumoPedidoViewModel
    {
        // Essas propriedades vêm prontas do servidor (GET); normalmente não queremos validá-las no POST.
        [ValidateNever] public CarrinhoModel? Carrinho { get; set; }
        [ValidateNever] public ClienteModel? Cliente { get; set; }
        [ValidateNever] public EnderecoModel? EnderecoEntrega { get; set; }
        [ValidateNever] public List<EnderecoModel> EnderecosDisponiveis { get; set; } = new();

        // Campos que o usuário preenche/seleciona na tela (serão bindados no POST)
        [Display(Name = "Endereço selecionado")]
        public int EnderecoSelecionadoId { get; set; }

        [Display(Name = "Forma de pagamento")]
        public string? MetodoPagamento { get; set; }

        // Usado no modal "Novo Endereço" (geralmente postado em ação separada)
        [ValidateNever] public EnderecoModel EnderecoNovo { get; set; } = new();

        [Display(Name = "Observações do pedido")]
        [StringLength(200)]
        public string? Observacoes { get; set; } = string.Empty;

        [Display(Name = "Retirar no local")]
        public bool RetiradaNoLocal { get; set; } = false;

        // Faixa/Modal "Confirme seus dados"
        public bool PrecisaConfirmar { get; set; } = false;

        // Para voltar ao Resumo após editar/confirmar dados
        public string? ReturnUrl { get; set; }
    }
}
