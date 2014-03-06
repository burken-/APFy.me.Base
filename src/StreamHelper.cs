using System.IO;

namespace MyAPI.Utilities
{
    public class StreamHelper
    {
        public static Stream StringToStream(string input) {
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            sw.Write(input);
            sw.Flush();

            ms.Position = 0;
            //byte[] b = Encoding.UTF8.GetBytes(input);
            //MemoryStream ms = new MemoryStream(b);
            //ms.Position = 0;
            return ms;
        }
    }
}
