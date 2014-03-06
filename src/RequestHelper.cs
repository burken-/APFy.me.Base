using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Collections;
using NLog;

namespace APFy.me.utilities
{
    public class RequestHelper
    {
        public static HttpWebRequest PrepareRequest(APISettings settings, HttpRequest request)
        {
            var editableQuery = new NameValueCollection(request.QueryString);
            var editableForm = new NameValueCollection(request.Form);

            string baseUrl = GetBaseUrl(settings, editableQuery, editableForm);

            //Append query to baseUrl
            if (settings.Verb.Equals("GET", StringComparison.CurrentCultureIgnoreCase))
            {
                UriBuilder ub = new UriBuilder(baseUrl);
                ub.Query = string.Format("{0}{1}", (string.IsNullOrEmpty(ub.Query)) ? "" : string.Format("{0}&", ub.Query.Substring(1)), PrepareRequestParameters(settings, editableQuery));
                baseUrl = ub.ToString();
            }

            HttpWebRequest req = GetBaseRequest(settings, request.Url.Scheme, baseUrl, request.Headers);

            //No matter which verb is being used, if message body exists we should try to forward it
            if (request.InputStream != null && request.InputStream.Length > 0)
            {
                string defaultType = request.Files.Count > 0 ? "multipart/form-data" : "application/x-www-form-urlencoded";
                string contentType = (request.Headers["Content-Type"] ?? defaultType).ToLower();
                if (contentType.Contains(";"))
                    contentType = contentType.Remove(contentType.IndexOf(';'));

                if ((new string[] { "multipart/form-data", "application/x-www-form-urlencoded" }).Contains(contentType.ToLower()))
                    SetRequestParameters(settings, req, request.Form, new HttpFileCollectionWrapper(request.Files), contentType);
                else
                    SetRequestParameters(settings, req, request.InputStream, contentType);
            }

            /*
            if (settings.Verb.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
            {
                string defaultType = request.Files.Count > 0 ? "multipart/form-data" : "application/x-www-form-urlencoded";
                string contentType = (request.Headers["Content-Type"] ?? defaultType).ToLower();
                if (contentType.Contains(";"))
                    contentType = contentType.Remove(contentType.IndexOf(';'));

                if ((new string[] { "multipart/form-data", "application/x-www-form-urlencoded" }).Contains(contentType.ToLower()))
                    SetRequestParameters(settings, req, request.Form, new HttpFileCollectionWrapper(request.Files), contentType);
                else
                    SetRequestParameters(settings, req, request.InputStream, contentType);
            }
            else if (settings.Verb.Equals("PUT", StringComparison.OrdinalIgnoreCase))
                SetRequestParameters(settings, req, request.InputStream, request.ContentType);
            */

            return req;
        }

        public static HttpWebRequest PrepareRequest(APISettings settings, string scheme, NameValueCollection headers, NameValueCollection paramCol, HttpFileCollectionBase fileCol, bool isRaw) {
            string baseUrl = settings.BaseUrl;
            UriBuilder ub = new UriBuilder(baseUrl);
            var editableQuery = new NameValueCollection(HttpUtility.ParseQueryString(ub.Query??""));
            MemoryStream ms = null;
            string contentType = headers["Content-Type"];

            //If delete or get, combine parameters with the base url's querystring. Form collection is always empty
            if ((new string[] { "get", "delete" }).Contains(settings.Verb.ToLower()))
            {
                foreach (string key in paramCol.Keys)
                {
                    editableQuery.Add(key, paramCol[key]);
                }

                //Empty the paramCol since it's only for post and put
                paramCol.Clear();

                ub.Query = string.Format("{0}{1}", (string.IsNullOrEmpty(ub.Query)) ? "" : string.Format("{0}&", ub.Query.Substring(1)), PrepareRequestParameters(settings, editableQuery));
                baseUrl = ub.ToString();
            }
            else if(isRaw) { 
                //Move information to a memorystream and clear the collection
                if (paramCol.Count > 1) {
                    ms = new MemoryStream(Encoding.UTF8.GetBytes(paramCol[0]));
                    paramCol.Clear();
                }
                else if (fileCol.Count > 1) {
                    ms = new MemoryStream();
                    fileCol[0].InputStream.CopyTo(ms);

                    if (string.IsNullOrEmpty(contentType))
                        contentType = fileCol[0].ContentType;
                }
            }

            baseUrl = GetBaseUrl(settings, editableQuery, paramCol);

            HttpWebRequest req = GetBaseRequest(settings, scheme, baseUrl, headers);

            if ((new string[] { "post", "put" }).Contains(settings.Verb.ToLower()))
            {
                if (contentType.Contains(";"))
                    contentType = contentType.Remove(contentType.IndexOf(';'));

                if (ms != null)
                    SetRequestParameters(settings, req, ms, (contentType??"").ToLower());
                else
                {
                    //For normal post, check if we should set default-content type.
                    string defaultType = fileCol.Count > 0 ? "multipart/form-data" : "application/x-www-form-urlencoded";
                    if (string.IsNullOrEmpty(contentType))
                        contentType = defaultType;

                    SetRequestParameters(settings, req, paramCol, fileCol, (contentType??"").ToLower());
                }
            }

            return req;
        }

        public static void LogRequest(HttpRequest request) {
            string body = "";

            if (request.InputStream.Length > 0) {
                StreamReader sr = new StreamReader(request.InputStream);
                body = sr.ReadToEnd();

                request.InputStream.Position = 0;
            }

            Logger requestLogger = LogManager.GetLogger("RequestResponseLog");
            LogEventInfo log = new LogEventInfo(LogLevel.Info, "RequestResponseLog", body);

            log.Properties["type"] = "Request";
            log.Properties["headers"] = string.Format("{0} {1} {2}\n{3}", request.HttpMethod, request.RawUrl, request.ServerVariables["SERVER_PROTOCOL"], request.ServerVariables["ALL_RAW"]);

            requestLogger.Log(log);
        }

        private static HttpWebRequest GetBaseRequest(APISettings settings, string scheme, string url, NameValueCollection headers) {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            //req.CookieContainer = new CookieContainer();
            req.Method = settings.Verb;
            SetRequestHeaders(settings, scheme, req, headers);
            req.AllowAutoRedirect = settings.FollowRedirects;
            req.MaximumAutomaticRedirections = 10;
            req.ReadWriteTimeout = 10000;
            req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            return req;
        }

        private static void SetRequestCookies(HttpWebRequest webRequest, string orgHeader) {
            if (string.IsNullOrWhiteSpace(orgHeader))
                return;

            var requestPath = webRequest.RequestUri.AbsolutePath;
            requestPath = Regex.Replace(requestPath, @"^(.*/)[^\.]\..*$", "$1");
            if (!requestPath.EndsWith("/"))
                requestPath = string.Format("{0}/",requestPath);

            Regex re = new Regex(@"&apfy.path=(?<path>.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            StringBuilder sb = new StringBuilder();
            var cookies = orgHeader.Split(';');
            foreach (var cookie in cookies) {
                
                var newCookie = cookie;
                var apfyPath = re.Match(newCookie);
                if (apfyPath.Success) { 
                    var actualPath = apfyPath.Groups["path"].Value;
                    if (!actualPath.EndsWith("/"))
                        actualPath = string.Format("{0}/",actualPath);

                    //Only append cookies with the correct path
                    if (!requestPath.StartsWith(actualPath))
                        newCookie = null;
                    else {
                        newCookie = re.Replace(newCookie, "");
                    }
                }

                if (!string.IsNullOrWhiteSpace(newCookie))
                    sb.AppendFormat("{0};",newCookie);
            }

            if (sb.Length > 0)
            {
                //Trim leaning ;
                sb.Length = sb.Length - 1;
                //Set cookie header
                webRequest.Headers["cookie"] = sb.ToString();
            }
        }

        private static void SetRequestCookies(HttpWebRequest webRequest, HttpCookieCollection cookies) {
            if (cookies.Count > 0) {
                CookieContainer cc = new CookieContainer();
                webRequest.CookieContainer = cc;
                Regex nameRegex = new Regex(@"[=;,\n\t\s]", RegexOptions.Compiled);
                Regex valueRegex = new Regex(@"[,;]", RegexOptions.Compiled);

                for (int i = 0; i < cookies.Count; i++)
                {
                    HttpCookie navigatorCookie = cookies[i];

                    //if (navigatorCookie.Domain != null)
                    //{
                    string path = string.Empty;

                    if (navigatorCookie.Values["apfy.path"] != null)
                    {
                        path = navigatorCookie.Values["apfy.path"];
                        navigatorCookie.Values.Remove("apfy.path");
                    }

                    //Make sure the cookies contains only valid values
                    string val = valueRegex.Replace(navigatorCookie.Value ?? "", m => HttpUtility.UrlEncode(m.Value));
                    string name = nameRegex.Replace(navigatorCookie.Name ?? "", m =>HttpUtility.UrlEncode(m.Value));

                    if (!string.IsNullOrEmpty(name) && !name.StartsWith("$") && val != null)
                    {
                        Cookie c = new Cookie(name, val);

                        c.Domain = webRequest.RequestUri.Host;

                        c.Expires = navigatorCookie.Expires;
                        c.HttpOnly = navigatorCookie.HttpOnly;

                        if (path != string.Empty)
                            c.Path = path;

                        c.Secure = (webRequest.RequestUri.Scheme == "https") ? true : false;
                        cc.Add(c);
                    }
                    //}
                }
            }
        }

        private static string GetBaseUrl(APISettings settings, NameValueCollection querystring, NameValueCollection form) {
            string tmpUrl = settings.BaseUrl;
            string pattern = @"\[(?<key>[^\]]+)\]";

            var matches = Regex.Matches(tmpUrl, pattern);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    string key = match.Groups["key"].Value;
                    //First check if we can find the value in the querystring, if not check the form collection
                    tmpUrl = Regex.Replace(tmpUrl, string.Format(@"\[{0}\]", key), querystring[key]??form[key]??"");
                    //Remove the key from querystring/form
                    querystring.Remove(key);
                    form.Remove(key);
                }
            }

            return tmpUrl;
        }

        private static string PrepareRequestParameters(APISettings settings, NameValueCollection requestParams) {
            string paramString = string.Empty;

            //var requestParams = settings.Verb.Equals("GET", StringComparison.CurrentCultureIgnoreCase)?request.QueryString:request.Form;

            paramString = string.Join("&", Array.ConvertAll(requestParams.AllKeys, key => {
                ParameterSettings tmpSetting = settings.RequestParameters.FirstOrDefault<ParameterSettings>(s => s.MappedKey != null && s.MappedKey.Equals(key, StringComparison.CurrentCultureIgnoreCase));
                return string.Format("{0}={1}", tmpSetting==null?key:tmpSetting.Key, HttpUtility.UrlEncode(requestParams[key]));
            }
            ));

            return paramString;
        }

        private static void SetRequestParameters(APISettings settings, HttpWebRequest webRequest, NameValueCollection orgPostCol, HttpFileCollectionBase orgFileCol, string contentType)
        {
            var requestStream = webRequest.GetRequestStream();

            byte[] requestBytes;

            if (contentType.Equals("application/x-www-form-urlencoded"))
            {
                webRequest.ContentType = contentType;
                requestBytes = Encoding.Default.GetBytes(PrepareRequestParameters(settings, orgPostCol));
                requestStream.Write(requestBytes, 0, requestBytes.Length);
            }
            else
            {
                string boundary = string.Format("----------------{0}", Guid.NewGuid().ToString("N"));
                byte[] boundaryBytes = Encoding.ASCII.GetBytes(string.Format("\r\n--{0}\r\n", boundary));
                byte[] trailerBytes = Encoding.ASCII.GetBytes(string.Format("\r\n--{0}--\r\n", boundary));

                webRequest.ContentType = string.Format("{0}; boundary={1}", contentType, boundary);

                string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
                string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";

                foreach (string key in orgPostCol.Keys)
                {
                    ParameterSettings tmpSetting = settings.RequestParameters.FirstOrDefault<ParameterSettings>(s => s.MappedKey != null && s.MappedKey.Equals(key, StringComparison.CurrentCultureIgnoreCase));

                    requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);
                    byte[] formBytes = Encoding.Default.GetBytes(string.Format(formdataTemplate, tmpSetting.Key==null?key:tmpSetting.Key, orgPostCol[key]));
                    requestStream.Write(formBytes, 0, formBytes.Length);
                }

                foreach (string key in orgFileCol.Keys)
                {
                    var file = orgFileCol[key];
                    if (file != null)
                    {
                        ParameterSettings tmpSetting = settings.RequestParameters.FirstOrDefault<ParameterSettings>(s => s.MappedKey != null && s.MappedKey.Equals(key, StringComparison.CurrentCultureIgnoreCase));

                        requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);
                        byte[] headerBytes = Encoding.Default.GetBytes(string.Format(headerTemplate, tmpSetting.Key==null?key:tmpSetting.Key, file.FileName, file.ContentType));
                        requestStream.Write(headerBytes, 0, headerBytes.Length);

                        file.InputStream.CopyTo(requestStream);
                    }
                }

                requestStream.Write(trailerBytes, 0, trailerBytes.Length);
            }

            requestStream.Close();
        }

        private static void SetRequestParameters(APISettings settings, HttpWebRequest webRequest, Stream orgPostStream, string contentType)
        {
            var requestStream = webRequest.GetRequestStream();
            orgPostStream.Position = 0;
            webRequest.ContentType = contentType;
            orgPostStream.CopyTo(requestStream);

            requestStream.Close();
        }

        private static void SetRequestHeaders(APISettings settings, string scheme, HttpWebRequest webRequest, NameValueCollection inputHeaders)
        {
            string prefix = ConfigurationManager.AppSettings["api.parameterPrefix"];

            //Skip api-headers and cookie header
            foreach (string header in inputHeaders.Keys)
            {
                if (WebHeaderCollection.IsRestricted(header))
                {
                    if (header.Equals("content-type", StringComparison.OrdinalIgnoreCase) && settings.Verb != "GET")
                        webRequest.ContentType = inputHeaders[header];
                    else if (header.Equals("accept", StringComparison.OrdinalIgnoreCase))
                        webRequest.Accept = inputHeaders[header];
                    //else if (header.Equals("expect", StringComparison.OrdinalIgnoreCase))
                    //    webRequest.Expect = inputHeaders[header];
                    else if (header.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
                        webRequest.UserAgent = inputHeaders[header];
                    else if (header.Equals("connection", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inputHeaders[header].Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
                            webRequest.KeepAlive = true;
                        else if (inputHeaders[header].Equals("close", StringComparison.OrdinalIgnoreCase))
                            webRequest.KeepAlive = false;
                        else
                            webRequest.Connection = inputHeaders[header];
                    }
                    else if (header.Equals("if-modified-since", StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime ifModifiedSince;
                        if (DateTime.TryParse(inputHeaders[header], out ifModifiedSince))
                            webRequest.IfModifiedSince = ifModifiedSince;
                    }
                    else if (header.Equals("referer", StringComparison.OrdinalIgnoreCase))
                        webRequest.Referer = inputHeaders[header];
                    else if (header.Equals("transfer-encoding", StringComparison.OrdinalIgnoreCase))
                        webRequest.TransferEncoding = inputHeaders[header];
                }
                else if (header.Equals("cookie", StringComparison.OrdinalIgnoreCase)) {
                    SetRequestCookies(webRequest, inputHeaders[header]);
                }
                else if ((webRequest.RequestUri.Host.ToLower() == "apfy.me" || webRequest.RequestUri.Host.ToLower() == "local.api.com" || !header.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)) && !new string[] { "x-forwarded-host", "x-forwarded-for", "via", "accept-encoding"/*, "cookie"*/ }.Contains(header.ToLower()))
                    webRequest.Headers.Add(header, inputHeaders[header]);
            }

            //if (webRequest.Method.Equals("POST", StringComparison.CurrentCultureIgnoreCase) && string.IsNullOrEmpty(webRequest.ContentType))
            //    webRequest.ContentType = "application/x-www-form-urlencoded";

            var ctx = HttpContext.Current;
            //Set proxy headers
            webRequest.Headers.Add("X-Forwarded-For", inputHeaders["x-forwarded-for"] == null ? ctx.Request.UserHostAddress : string.Format("{0}, {1}", inputHeaders["x-forwarded-for"], ctx.Request.UserHostAddress));
            webRequest.Headers.Add("X-Forwarded-Proto", scheme);
            //webRequest.Headers.Add("X-Forwarded-Host", inputHeaders["x-forwarded-Host"] == null ? ctx.Request.Url.Host : string.Format("{0}, {1}", inputHeaders["x-forwarded-host"], ctx.Request.Url.Host));
            webRequest.Headers.Add("Via", string.Format("{0} MyApi (MyApi.com 1.0)", ctx.Request.ServerVariables["SERVER_PROTOCOL"]));
        }
    }
}