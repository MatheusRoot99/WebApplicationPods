

namespace WebApplicationPods.Models
{
    public class ResumoPedidoViewModel
    {
        public CarrinhoModel Carrinho { get; set; }
        public ClienteModel Cliente { get; set; }
        public EnderecoModel EnderecoEntrega { get; set; }
        public List<EnderecoModel> EnderecosDisponiveis { get; set; } = new List<EnderecoModel>();

        // Campos para seleção na view
        public int EnderecoSelecionadoId { get; set; }
        public string MetodoPagamento { get; set; }
    }
}
