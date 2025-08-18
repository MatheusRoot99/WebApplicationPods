using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Repository.Repository
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly BancoContext _context;

        public ClienteRepository(BancoContext context)
        {
            _context = context;
        }

        public ClienteModel ObterPorTelefone(string telefone)
        {
            telefone = new string((telefone ?? "").Where(char.IsDigit).ToArray());

            return _context.Clientes
                .Include(c => c.Enderecos)
                .Include(c => c.Pedidos)
                .FirstOrDefault(c => c.Telefone == telefone);
        }

        public ClienteModel ObterPorId(int id)
        {
            return _context.Clientes
                .Include(c => c.Enderecos)
                .Include(c => c.Pedidos)
                .FirstOrDefault(c => c.Id == id);
        }

        public void Adicionar(ClienteModel cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente.Telefone))
                throw new ArgumentException("Telefone é obrigatório");

            cliente.Telefone = new string(cliente.Telefone.Where(char.IsDigit).ToArray());

            _context.Clientes.Add(cliente);
            _context.SaveChanges();
        }

        public void Atualizar(ClienteModel cliente)
        {
            cliente.Telefone = new string((cliente.Telefone ?? "").Where(char.IsDigit).ToArray());

            _context.Clientes.Update(cliente);
            _context.SaveChanges();
        }

        public bool TelefoneExiste(string telefone)
        {
            telefone = new string((telefone ?? "").Where(char.IsDigit).ToArray());
            return _context.Clientes.Any(c => c.Telefone == telefone);
        }

        public EnderecoModel ObterEnderecoPrincipal(int clienteId)
        {
            return _context.Enderecos
                .FirstOrDefault(e => e.ClienteId == clienteId && e.Principal);
        }

        public IEnumerable<EnderecoModel> ObterEnderecos(int clienteId)
        {
            return _context.Enderecos
                .Where(e => e.ClienteId == clienteId)
                .AsNoTracking()
                .ToList();
        }

        // 🔽 novos métodos

        public EnderecoModel AdicionarEndereco(int clienteId, EnderecoModel endereco)
        {
            endereco.ClienteId = clienteId;

            var dig = new string((endereco.CEP ?? "").Where(char.IsDigit).ToArray());
            if (dig.Length == 8) endereco.CEP = $"{dig[..5]}-{dig[5..]}";

            _context.Enderecos.Add(endereco);
            _context.SaveChanges();
            return endereco;
        }

        public void DefinirEnderecoPrincipal(int clienteId, int enderecoId)
        {
            var enderecos = _context.Enderecos.Where(e => e.ClienteId == clienteId).ToList();
            foreach (var e in enderecos) e.Principal = (e.Id == enderecoId);
            _context.SaveChanges();
        }

        public EnderecoModel ObterEnderecoPorId(int enderecoId)
        {
            return _context.Enderecos.FirstOrDefault(e => e.Id == enderecoId);
        }

        public EnderecoModel AtualizarEndereco(EnderecoModel endereco)
        {
            var existente = _context.Enderecos.FirstOrDefault(e => e.Id == endereco.Id);
            if (existente == null) throw new Exception("Endereço não encontrado.");

            existente.Logradouro = endereco.Logradouro;
            existente.Numero = endereco.Numero;
            existente.Complemento = endereco.Complemento;
            existente.Bairro = endereco.Bairro;
            existente.Cidade = endereco.Cidade;
            existente.Estado = (endereco.Estado ?? "").ToUpper();

            var num = new string((endereco.CEP ?? "").Where(char.IsDigit).ToArray());
            if (num.Length == 8) existente.CEP = $"{num.Substring(0, 5)}-{num.Substring(5, 3)}";
            else existente.CEP = endereco.CEP;

            // o “Principal” será ajustado pela DefinirEnderecoPrincipal, se necessário
            _context.SaveChanges();
            return existente;
        }

        // utilitário
        private static string FormatarCep(string? cep)
        {
            var dig = new string((cep ?? "").Where(char.IsDigit).ToArray());
            if (dig.Length == 8)
                return $"{dig.Substring(0, 5)}-{dig.Substring(5, 3)}";
            return cep ?? "";
        }
    }
}
