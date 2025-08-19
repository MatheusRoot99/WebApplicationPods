using System.Linq;

namespace WebApplicationPods.Utils
{
    public static class CpfValidator
    {
        public static bool EhCpfValido(string? cpf)
        {
            cpf = cpf.ApenasDigitos();
            if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11) return false;
            if (new string(cpf[0], 11) == cpf) return false; // todos iguais

            int CalcDig(string src, int len)
            {
                var soma = 0;
                for (int i = 0; i < len; i++)
                    soma += (src[i] - '0') * (len + 1 - i);
                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }

            var d1 = CalcDig(cpf, 9);
            var d2 = CalcDig(cpf, 10);
            return d1 == (cpf[9] - '0') && d2 == (cpf[10] - '0');
        }
    }
}
