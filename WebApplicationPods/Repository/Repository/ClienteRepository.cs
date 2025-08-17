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
            // Remove formatação do telefone para busca
            telefone = new string(telefone.Where(char.IsDigit).ToArray());

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
            // Validação básica
            if (string.IsNullOrWhiteSpace(cliente.Telefone))
                throw new ArgumentException("Telefone é obrigatório");

            cliente.Telefone = new string(cliente.Telefone.Where(char.IsDigit).ToArray());

            _context.Clientes.Add(cliente);
            _context.SaveChanges();
        }

        public void Atualizar(ClienteModel cliente)
        {
            cliente.Telefone = new string(cliente.Telefone.Where(char.IsDigit).ToArray());

            _context.Clientes.Update(cliente);
            _context.SaveChanges();
        }

        public bool TelefoneExiste(string telefone)
        {
            telefone = new string(telefone.Where(char.IsDigit).ToArray());
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
                .ToList();
        }
    }
}
