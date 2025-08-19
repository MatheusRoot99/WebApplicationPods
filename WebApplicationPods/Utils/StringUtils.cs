using System.Linq;

namespace WebApplicationPods.Utils
{
    public static class StringUtils
    {
        public static string ApenasDigitos(this string s)
            => new string((s ?? "").Where(char.IsDigit).ToArray());
    }
}
