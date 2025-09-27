using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WebApplicationPods.Validation
{
    /// <summary>
    /// Valida CPF brasileiro. Aceita com/sem máscara; armazene só dígitos.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class CpfAttribute : ValidationAttribute
    {
        public CpfAttribute() : base("CPF inválido.") { }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
                return ValidationResult.Success; // deixe [Required] cuidar da obrigatoriedade

            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length != 11)
                return new ValidationResult(ErrorMessage);

            // Rejeita CPFs com todos os dígitos iguais (000..., 111..., etc.)
            if (digits.Distinct().Count() == 1)
                return new ValidationResult(ErrorMessage);

            // Cálculo dos DVs
            bool CheckDV(string src, int dvPos)
            {
                int[] mult = dvPos == 9
                    ? new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 }
                    : new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

                var slice = src.Take(mult.Length).Select(c => c - '0').ToArray();
                int sum = 0;
                for (int i = 0; i < mult.Length; i++) sum += slice[i] * mult[i];
                int mod = sum % 11;
                int dv = mod < 2 ? 0 : 11 - mod;
                return dv == (src[dvPos] - '0');
            }

            if (!CheckDV(digits, 9)) return new ValidationResult(ErrorMessage);
            if (!CheckDV(digits, 10)) return new ValidationResult(ErrorMessage);

            return ValidationResult.Success;
        }
    }
}
