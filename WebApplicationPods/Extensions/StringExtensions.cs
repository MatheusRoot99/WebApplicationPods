using System.Text.RegularExpressions;

namespace WebApplicationPods.Extensions
{
    public static class StringExtensions
    {
        public static string ApenasDigitos(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return new string(input.Where(char.IsDigit).ToArray());
        }

        public static bool IsValidEmail(this string email)
        {
            if (string.IsNullOrEmpty(email))
                return false;

            // Regex simples para validar email
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            return regex.IsMatch(email);
        }
    }
}