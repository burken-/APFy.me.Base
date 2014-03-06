using System.Text;
using System.Text.RegularExpressions;

namespace APFy.me.utilities
{
    public class Base64Helper
    {
        public static bool IsBase64(string str)
        {
            return Regex.IsMatch(Regex.Replace(str??"","\\s",""), "^([A-Za-z0-9+/]{4})*([A-Za-z0-9+/]{4}|[A-Za-z0-9+/]{3}=|[A-Za-z0-9+/]{2}==)$");
        }

        public static string Base64ToString(string str, Encoding enc) {
            if (enc == null)
                enc = Encoding.Default;

            byte[] encodedDataAsBytes = System.Convert.FromBase64String(str);

            return enc.GetString(encodedDataAsBytes);
        }

        public static string StringToBase64(string str, Encoding enc) {
            if (enc == null)
                enc = Encoding.Default;

            byte[] bytes = enc.GetBytes(str);
            return System.Convert.ToBase64String(bytes, System.Base64FormattingOptions.InsertLineBreaks);
        }
    }
}