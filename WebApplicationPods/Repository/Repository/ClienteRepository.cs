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

        #region Helpers

        private static string SoDigitos(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());
        private static string FormatarCep(string? cep)
        {
            var dig = SoDigitos(cep);
            return dig.Length == 8 ? $"{dig[..5]}-{dig[5..]}" : (cep ?? "");
        }
        private static string UF(string? uf) => (uf ?? "").Trim().ToUpperInvariant();

        #endregion

        // ======================= CLIENTE =======================

        public ClienteModel ObterPorTelefone(string telefone)
        {
            telefone = SoDigitos(telefone);
            return _context.Clientes
                .Include(c => c.Enderecos) // filtrados por QueryFilter (Ativo = true)
                .Include(c => c.Pedidos)
                .FirstOrDefault(c => c.Telefone == telefone);
        }

        public ClienteModel ObterPorId(int id)
        {
            return _context.Clientes
                .Include(c => c.Enderecos) // filtrados por QueryFilter (Ativo = true)
                .Include(c => c.Pedidos)
                .FirstOrDefault(c => c.Id == id);
        }

        public void Adicionar(ClienteModel cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente.Telefone))
                throw new ArgumentException("Telefone é obrigatório");

            cliente.Telefone = SoDigitos(cliente.Telefone);

            _context.Clientes.Add(cliente);
            _context.SaveChanges();
        }

        public void Atualizar(ClienteModel cliente)
        {
            cliente.Telefone = SoDigitos(cliente.Telefone);
            _context.Clientes.Update(cliente);
            _context.SaveChanges();
        }

        public bool TelefoneExiste(string telefone)
        {
            telefone = SoDigitos(telefone);
            return _context.Clientes.Any(c => c.Telefone == telefone);
        }

        // ======================= ENDEREÇOS =======================

        public IEnumerable<EnderecoModel> ObterEnderecos(int clienteId)
        {
            // QueryFilter já aplica e.Ativo == true
            return _context.Enderecos
                .Where(e => e.ClienteId == clienteId)
                .AsNoTracking()
                .ToList();
        }

        public EnderecoModel ObterEnderecoPrincipal(int clienteId)
        {
            return _context.Enderecos
                .FirstOrDefault(e => e.ClienteId == clienteId && e.Principal);
        }

        /// <summary>
        /// Adiciona endereço. Se for o primeiro ou marcado como principal, promove atômico.
        /// </summary>
        public EnderecoModel AdicionarEndereco(int clienteId, EnderecoModel endereco)
        {
            endereco.ClienteId = clienteId;
            endereco.CEP = FormatarCep(endereco.CEP);
            endereco.Estado = UF(endereco.Estado);

            // vai ser principal se veio marcado OU se não há nenhum ativo
            bool deveSerPrincipal = endereco.Principal ||
                                    !_context.Enderecos.Any(e => e.ClienteId == clienteId && e.Ativo);

            // salva como não principal para evitar conflito com índice
            endereco.Principal = false;
            endereco.Ativo = true;

            _context.Enderecos.Add(endereco);
            _context.SaveChanges();

            if (deveSerPrincipal)
            {
                DefinirEnderecoPrincipal(clienteId, endereco.Id);
                endereco.Principal = true;
            }

            return endereco;
        }

        /// <summary>
        /// Garante exatamente 1 principal ENTRE OS ATIVOS, em UM UPDATE.
        /// </summary>
        public void DefinirEnderecoPrincipal(int clienteId, int enderecoId)
        {
            using var tx = _context.Database.BeginTransaction();
            _context.Database.ExecuteSqlRaw(@"
                UPDATE Enderecos
                   SET Principal = CASE WHEN Id = {0} THEN 1 ELSE 0 END
                 WHERE ClienteId = {1} AND Ativo = 1;",
                enderecoId, clienteId);
            tx.Commit();
        }

        public EnderecoModel ObterEnderecoPorId(int enderecoId)
        {
            // QueryFilter aplica Ativo; se precisar buscar inativos, use IgnoreQueryFilters() aqui.
            return _context.Enderecos.FirstOrDefault(e => e.Id == enderecoId);
        }

        /// <summary>
        /// Atualiza campos do endereço (sem mexer no principal). Para trocar principal, use AtualizarEnderecoComPrincipal.
        /// </summary>
        public EnderecoModel AtualizarEndereco(EnderecoModel endereco)
        {
            var existente = _context.Enderecos.FirstOrDefault(e => e.Id == endereco.Id && e.ClienteId == endereco.ClienteId);
            if (existente == null) throw new Exception("Endereço não encontrado.");

            existente.Logradouro = endereco.Logradouro;
            existente.Numero = endereco.Numero;
            existente.Complemento = endereco.Complemento;
            existente.Bairro = endereco.Bairro;
            existente.Cidade = endereco.Cidade;
            existente.Estado = UF(endereco.Estado);
            existente.CEP = FormatarCep(endereco.CEP);
            existente.Ativo = true; // garante ativo ao editar

            _context.SaveChanges();
            return existente;
        }

        /// <summary>
        /// Atualiza campos e realiza troca atômica do principal se solicitado.
        /// </summary>
        public EnderecoModel AtualizarEnderecoComPrincipal(EnderecoModel endereco)
        {
            using var tx = _context.Database.BeginTransaction();

            var existente = _context.Enderecos
                .FirstOrDefault(e => e.Id == endereco.Id && e.ClienteId == endereco.ClienteId);
            if (existente == null) throw new Exception("Endereço não encontrado.");

            existente.Logradouro = endereco.Logradouro;
            existente.Numero = endereco.Numero;
            existente.Complemento = endereco.Complemento;
            existente.Bairro = endereco.Bairro;
            existente.Cidade = endereco.Cidade;
            existente.Estado = UF(endereco.Estado);
            existente.CEP = FormatarCep(endereco.CEP);
            existente.Ativo = true;

            _context.SaveChanges();

            if (endereco.Principal)
            {
                DefinirEnderecoPrincipal(existente.ClienteId, existente.Id);
                existente.Principal = true;
            }
            else
            {
                _context.Database.ExecuteSqlRaw(@"
                    UPDATE Enderecos
                       SET Principal = 0
                     WHERE Id = {0} AND ClienteId = {1};",
                    existente.Id, existente.ClienteId);
                existente.Principal = false;
            }

            tx.Commit();
            return existente;
        }

        /// <summary>
        /// Corrige inconsistências antigas: se houver mais de um principal, mantém o menor Id.
        /// </summary>
        public void GarantirApenasUmEnderecoPrincipal(int clienteId, int enderecoIdExcluido = 0)
        {
            var principais = _context.Enderecos
                .Where(e => e.ClienteId == clienteId && e.Principal && e.Id != enderecoIdExcluido)
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .ToList();

            if (principais.Count > 1)
            {
                var manter = principais.First();
                DefinirEnderecoPrincipal(clienteId, manter);
            }
        }

        /// <summary>
        /// Soft delete: marca Ativo=false e zera Principal. Se era principal, elege outro ativo.
        /// Retorna Id do novo principal (se houver).
        /// </summary>
        public int? RemoverEndereco(int clienteId, int enderecoId)
        {
            using var tx = _context.Database.BeginTransaction();

            // Aqui precisamos IGNORAR o QueryFilter para conseguir pegar um endereço mesmo que já tenha sido desativado (caso de reentrância)
            var alvo = _context.Enderecos
                .IgnoreQueryFilters()
                .FirstOrDefault(e => e.Id == enderecoId && e.ClienteId == clienteId);

            if (alvo == null) throw new Exception("Endereço não encontrado.");

            if (!alvo.Ativo)
            {
                tx.Commit();
                return null; // já estava inativo
            }

            bool eraPrincipal = alvo.Principal;

            // Soft delete
            alvo.Ativo = false;
            alvo.Principal = false;
            _context.SaveChanges();

            int? novoPrincipalId = null;

            if (eraPrincipal)
            {
                novoPrincipalId = _context.Enderecos
                    .Where(e => e.ClienteId == clienteId && e.Ativo)
                    .OrderBy(e => e.Id)
                    .Select(e => (int?)e.Id)
                    .FirstOrDefault();

                if (novoPrincipalId.HasValue)
                {
                    DefinirEnderecoPrincipal(clienteId, novoPrincipalId.Value);
                }
            }

            tx.Commit();
            return novoPrincipalId;
        }
    }
}
