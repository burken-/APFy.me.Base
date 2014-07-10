using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Sgml;

namespace APFy.me.utilities
{
    public class ContentConverter
    {
        public static XmlReader ConvertContent(Stream content, string contentType, string charSet, string wrapperNode) {
            return ContentConverter.ConvertContent(content, contentType, charSet, wrapperNode, XmlHelper.DefaultReaderSettings);
        }

        public static XmlReader ConvertContent(Stream content, string contentType, string charSet, string wrapperNode, XmlReaderSettings settings) {
            Encoding encoding = Encoding.Default;

            contentType = contentType ?? "";
            charSet = charSet ?? "";

            if (string.IsNullOrWhiteSpace(wrapperNode))
                wrapperNode = null;

            string fallbackCharset = charSet;

            if (contentType.Contains(";"))
            {
                if(contentType.ToLowerInvariant().Contains("charset"))
                    fallbackCharset = contentType.Substring(contentType.IndexOf("charset=", StringComparison.OrdinalIgnoreCase) + 8);

                contentType = contentType.Remove(contentType.IndexOf(";"));
            }

            if (!TryReadCharset(charSet, out encoding) && !charSet.Equals(fallbackCharset, StringComparison.OrdinalIgnoreCase))
                TryReadCharset(fallbackCharset, out encoding);

            if (contentType.Contains("text/html"))
                return ConvertHtmlToXml(content, wrapperNode, encoding, settings);
            else if (Regex.IsMatch(contentType, @"(text/xml|application/([^\+]+\+)?xml)"))
            {
                XmlReader reader = XmlReader.Create(new StreamReader(content, encoding), settings);
                reader.MoveToContent();
                return reader;
            }
            else if (contentType.Contains("application/json"))
                return ConvertJsonToXml(content, wrapperNode, encoding, settings);
            else if (contentType.StartsWith("text/") || contentType.Equals("application/x-javascript", StringComparison.OrdinalIgnoreCase) || contentType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase))
                return ConvertTextToXml(content, contentType, wrapperNode, encoding, settings);
            else
                return ConvertBinaryToXml(content, contentType, wrapperNode, encoding, settings);
        }

        private static bool TryReadCharset(string charSet, out Encoding enc) {
            charSet = (charSet??"").Replace("'", "").Replace("\"", "").Trim();

            if (string.IsNullOrEmpty(charSet)) {
                enc = Encoding.Default;
                return false;
            }

            try
            {
                charSet = charSet.Replace("'", "").Replace("\"", "").Trim();
                enc = Encoding.GetEncoding(charSet);
                return true;
            }
            catch (Exception e)
            {
                enc = Encoding.Default;
                return false;
            }
        }

        public static XmlReader ConvertHtmlToXml(Stream content, string wrapperNode, Encoding encoding, XmlReaderSettings settings) {
            XmlNameTable nt = new NameTable();
            nt.Add("html");
            nt.Add("head");
            nt.Add("title");
            nt.Add("meta");
            nt.Add("body");
            nt.Add("div");
            nt.Add("span");
            nt.Add("p");
            nt.Add("script");
            nt.Add("table");
            nt.Add("thead");
            nt.Add("tbody");
            nt.Add("tr");
            nt.Add("td");
            nt.Add("th");
            nt.Add("b");
            nt.Add("strong");
            nt.Add("i");
            nt.Add("em");
            nt.Add("ul");
            nt.Add("ol");
            nt.Add("li");
            nt.Add("a");
            nt.Add("input");
            nt.Add("select");
            nt.Add("textarea");
            nt.Add("header");
            nt.Add("footer");
            nt.Add("section");
            nt.Add("abbr");
            nt.Add("address");
            nt.Add("area");
            nt.Add("article");
            nt.Add("blockquote");
            nt.Add("br");
            nt.Add("button");
            nt.Add("canvas");
            nt.Add("caption");
            nt.Add("cite");
            nt.Add("code");
            nt.Add("pre");
            nt.Add("dd");
            nt.Add("dl");
            nt.Add("dt");
            nt.Add("fieldset");
            nt.Add("legend");
            nt.Add("form");
            nt.Add("h1");
            nt.Add("h2");
            nt.Add("h3");
            nt.Add("h4");
            nt.Add("h5");
            nt.Add("h6");
            nt.Add("hr");
            nt.Add("img");
            nt.Add("link");
            nt.Add("param");
            nt.Add("font");

            nt.Add("style");
            nt.Add("accesskey");
            nt.Add("class");
            nt.Add("id");
            nt.Add("name");
            nt.Add("rel");
            nt.Add("lang");
            nt.Add("cols");
            nt.Add("rows");
            nt.Add("width");
            nt.Add("height");
            nt.Add("value");
            nt.Add("href");
            nt.Add("content");
            nt.Add("http-equiv");
            nt.Add("src");
            nt.Add("border");
            nt.Add("colspan");
            nt.Add("type");
            nt.Add("onclick");
            nt.Add("onblur");
            nt.Add("onfocus");
            nt.Add("target");
            nt.Add("valign");
            nt.Add("align");
            nt.Add("action");
            nt.Add("method");
            nt.Add("alt");
            nt.Add("bgcolor");

            StreamReader sr = null;

            if (!string.IsNullOrWhiteSpace(wrapperNode))
            {
                sr = new StreamReader(content, encoding);
                //Read response to memory, add the wrapping node and write to memory stream
                string inputContent = string.Format("<{0}>{1}</{0}>", wrapperNode.Trim(), sr.ReadToEnd());
                MemoryStream ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms, encoding);
                sw.Write(inputContent);
                sw.Flush();
                sw.Close();
                sw.Dispose();

                ms.Position = 0;
                sr = new StreamReader(ms, encoding);
            }
            else {
                MemoryStream ms = new MemoryStream();
                content.CopyTo(ms);
                ms.Position = 0;
                sr = new StreamReader(ms, encoding);
            }

            SgmlReader r = new SgmlReader(nt);
            r.DocType = "HTML";
            r.IgnoreDtd = true;
            r.SkipDefaultNamespace = true;
            
            r.WhitespaceHandling = WhitespaceHandling.Significant;
            r.CaseFolding = CaseFolding.ToLower;
            r.InputStream = sr;

            r.MoveToContent();

            return r;
        }

        public static XmlReader ConvertTextToXml(Stream content, string contentType, string wrapperNode, Encoding encoding, XmlReaderSettings settings)
        {
            MemoryStream ms = new MemoryStream();
            XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { OmitXmlDeclaration = true });

            StreamReader sr = new StreamReader(content, encoding);

            writer.WriteStartElement((wrapperNode??"content").Trim());
            writer.WriteAttributeString("content-type", contentType);
            writer.WriteCData(sr.ReadToEnd());
            writer.WriteEndElement();
            writer.Flush();
            writer.Close();

            ms.Position = 0;
            XmlReader reader = XmlReader.Create(ms, settings);
            reader.MoveToContent();
            return reader;
        }

        public static XmlReader ConvertBinaryToXml(Stream content, string contentType, string wrapperNode, Encoding encoding, XmlReaderSettings settings)
        {
            int bufferSize = 1024;
            int readBytes = 0;
            byte[] buffer = new byte[bufferSize];

            MemoryStream ms = new MemoryStream();
            XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { OmitXmlDeclaration = true });

            BinaryReader br = new BinaryReader(content, encoding);

            writer.WriteStartElement((wrapperNode??"content").Trim());
            writer.WriteAttributeString("content-type", contentType);
            do
            {
                readBytes = br.Read(buffer, 0, bufferSize);
                writer.WriteBase64(buffer, 0, readBytes);
            } while (bufferSize <= readBytes);

            writer.WriteEndElement();
            writer.Flush();
            writer.Close();

            ms.Position = 0;

            XmlReader reader = XmlReader.Create(ms, settings);
            reader.MoveToContent();
            return reader;
        }

        public static XmlReader ConvertJsonToXml(Stream content, string wrapperNode, Encoding encoding, XmlReaderSettings readerSettings)
        {
            MemoryStream ms = new MemoryStream();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            XmlWriter writer = XmlWriter.Create(ms, settings);
            StreamReader sr = new StreamReader(content, encoding);
            string jsonContent = sr.ReadToEnd();

            if(jsonContent.StartsWith("["))
                jsonContent = string.Concat("{array:", jsonContent, '}');

            XmlDocument doc = null;

            doc = JsonConvert.DeserializeXmlNode(jsonContent, (wrapperNode??"content").Trim());

            doc.WriteTo(writer);
            writer.Flush();
            writer.Close();

            ms.Position = 0;
            XmlReader reader = XmlReader.Create(ms, readerSettings);
            reader.MoveToContent();
            return reader;
        }

        public static string ConvertXmlToJson(string xml) {
            if (!xml.StartsWith("<"))
                xml = xml.Substring(xml.IndexOf('<'));

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            
            return JsonConvert.SerializeXmlNode(doc);
        }
    }
}