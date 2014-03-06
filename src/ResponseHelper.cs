using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web;
using NLog;
using System.Text.RegularExpressions;

namespace APFy.me.utilities
{
    public class ResponseHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static HttpWebResponse PrepareResponse(HttpWebRequest request, out Exception exception) {
            HttpWebResponse webResponse = null;
            exception = null;

            try
            {
                webResponse = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                webResponse = (HttpWebResponse)e.Response;
                exception = e;
                logger.Error(e.ToString());
            }
            catch (Exception e)
            {
                exception = e;
                logger.Error(e.ToString());
            }

            return webResponse;
        }

        public static void SetResponseCookies(HttpWebResponse webResponse, HttpResponse response, HttpRequest request) {

            var orgHeader = webResponse.Headers["set-cookie"];
            if (string.IsNullOrWhiteSpace(orgHeader))
                return;

            //Replace the domain
            orgHeader = Regex.Replace(orgHeader, @"\bdomain=[^;]*", string.Format("domain={0}", request.Url.Host), RegexOptions.IgnoreCase);

            //Replace the path
            orgHeader = Regex.Replace(orgHeader, @"(?<=^|(?<!expires=[^,]{3}),)(?<key>[^=]+)=(?<value>[^;]+)(?<otherParts>.*?);\s*path=(?<path>[^;]*)", m =>
            {
                var orgPath = m.Groups["path"];
                var orgKey = m.Groups["key"];
                var orgValue = m.Groups["value"];
                var orgParts = m.Groups["otherParts"];

                var newPath = "/";
                if (request.Url.Host == ConfigurationManager.AppSettings["api.domain"])
                    newPath = request.Url.PathAndQuery.Substring(0, request.Url.PathAndQuery.IndexOf('/', 1));

                var newValue = string.Format("{0}&apfy.path={1}", orgValue, orgPath);

                return string.Format("{0}={1}{2};path={3}", orgKey, newValue, orgParts, newPath);
            }, RegexOptions.IgnoreCase);

            response.Headers["set-cookie"] = orgHeader;

            /*
            response.Cookies.Clear();

            CookieCollection c = CookieParser.GetAllCookiesFromHeader(webResponse.Headers["set-cookie"], request.Url.Host);

            foreach (Cookie cookie in c)
            {
                HttpCookie tmpCookie = new HttpCookie(cookie.Name);

                //Copy Porperties
                tmpCookie.Value = cookie.Value;
                tmpCookie.Domain = cookie.Domain;
                tmpCookie.Expires = cookie.Expires;
                tmpCookie.HttpOnly = cookie.HttpOnly;
                tmpCookie.Secure = cookie.Secure;

                if (!string.IsNullOrEmpty(cookie.Path))
                    tmpCookie.Values["apfy.path"] = cookie.Path;

                if (request.Url.Host == ConfigurationManager.AppSettings["api.domain"])
                    tmpCookie.Path = request.Url.PathAndQuery.Substring(0, request.Url.PathAndQuery.IndexOf('/', 1));
                else
                    tmpCookie.Path = "/";

                //cookie.Secure = request.IsSecureConnection;
                response.Cookies.Add(tmpCookie);
            }
            */
            /*foreach (Cookie receivedCookie in webRequest.CookieContainer.GetCookies(webRequest.RequestUri))
            {
                string path = string.Empty;

                HttpCookie c = new HttpCookie(receivedCookie.Name, receivedCookie.Value);
                if (!string.IsNullOrEmpty(receivedCookie.Path)) {
                    c.Values["apfy.path"] = receivedCookie.Path;
                }


                if(!string.IsNullOrEmpty(receivedCookie.Domain))
                    c.Domain = request.Url.Host;

                c.Expires = receivedCookie.Expires;
                c.HttpOnly = receivedCookie.HttpOnly;

                if (request.Url.Host == ConfigurationManager.AppSettings["api.domain"])
                    c.Path = request.Url.PathAndQuery.Substring(0, request.Url.PathAndQuery.IndexOf('/',1));
                else
                    c.Path = "/";
                
                c.Secure = request.IsSecureConnection;
                response.Cookies.Add(c);
            }*/
        }

        public static void SetResponseHeaders(HttpWebResponse webResponse, HttpResponse response) {
            bool headerSet;
            foreach (string header in webResponse.Headers.Keys) {
                headerSet = false;
                if (!(WebHeaderCollection.IsRestricted(header) || new string[]{"cookie"}.Contains(header.ToLower()))) {
                    try
                    {
                        response.Headers.Add(header, webResponse.Headers[header]);
                        headerSet = true;
                    }catch(Exception e){
                        logger.Error(e.ToString());
                        // do nothing with restricted headers
                    }
                }

                /*if (!headerSet)
                {
                    if (header.Equals("cache-control", StringComparison.OrdinalIgnoreCase))
                        response.Cache.
                        response.CacheControl = webResponse.Headers[header];
                }*/
            }

            response.Headers.Add("Via", "1.1 MyApi (MyApi.com 1.0)");
        }

        public static string LogResponse(string body) {
            HttpResponse res = HttpContext.Current.Response;

            Logger responseLogger = LogManager.GetLogger("RequestResponseLog");
            LogEventInfo log = new LogEventInfo(LogLevel.Info, "RequestResponseLog", body);

            //response.Headers // loop through these "key: value"

            log.Properties["type"] = "Response";
            log.Properties["headers"] = string.Format("HTTP/1.1 {0}\n{1}", res.Status, string.Join("\n", Array.ConvertAll(res.Headers.AllKeys, key => { return string.Format("{0}: {1}", key, res.Headers[key]); })));

            responseLogger.Log(log);

            return body;
        }
    }
}