// Mapping/ClienteMappings.cs
using System.Text.RegularExpressions;
using WebApplicationPods.Models;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Mapping
{
    public static class ClienteMappings
    {
        private static string OnlyDigits(string? s) => Regex.Replace(s ?? "", "[^0-9]", "");

        private static string FormatCpf(string? cpfDigits)
        {
            var d = OnlyDigits(cpfDigits);
            if (d.Length != 11) return cpfDigits ?? string.Empty;
            return $"{d[..3]}.{d.Substring(3, 3)}.{d.Substring(6, 3)}-{d.Substring(9, 2)}";
        }

        private static string FormatPhoneBr(string? tel)
        {
            var d = OnlyDigits(tel);
            if (d.Length == 11) return $"({d[..2]}) {d.Substring(2, 5)}-{d.Substring(7)}";
            if (d.Length == 10) return $"({d[..2]}) {d.Substring(2, 4)}-{d.Substring(6)}";
            return tel ?? string.Empty;
        }

        public static ClienteViewModel ToViewModel(this ClienteModel m)
        {
            return new ClienteViewModel
            {
                Id = m.Id,
                Nome = m.Nome ?? "",
                Email = m.Email ?? "",
                Telefone = FormatPhoneBr(m.Telefone),
                CPF = FormatCpf(m.Cpf),
                DataNascimento = m.DataNascimento,
                DataCadastro = m.DataCadastro
            };
        }

        /// <summary>
        /// Atualiza o Model com dados do ViewModel (edição).
        /// Obs.: não altera DataCadastro.
        /// </summary>
        public static void UpdateFromViewModel(this ClienteModel m, ClienteViewModel vm)
        {
            m.Nome = (vm.Nome ?? "").Trim();
            m.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
            m.Telefone = OnlyDigits(vm.Telefone);
            m.Cpf = OnlyDigits(vm.CPF); // sempre salvar apenas dígitos
            m.DataNascimento = vm.DataNascimento;
        }

        /// <summary>
        /// Cria um novo Model a partir do ViewModel (cadastro).
        /// </summary>
        public static ClienteModel ToModelNew(this ClienteViewModel vm)
        {
            return new ClienteModel
            {
                Nome = (vm.Nome ?? "").Trim(),
                Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
                Telefone = OnlyDigits(vm.Telefone),
                Cpf = OnlyDigits(vm.CPF),
                DataNascimento = vm.DataNascimento,
                DataCadastro = DateTime.Now
            };
        }
    }
}
