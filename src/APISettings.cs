using System;
using System.Collections.Generic;
using System.Linq;
//using MyApiUtilities;

namespace APFy.me.utilities
{
    public class APISettings
    {
        public int Id;
        //public string OfficialDomain { get; set; }
        public string BaseUrl{ get; set; }
        public string Verb { get; set; }
        public bool RequireHttps { get; set; }
        public bool FollowRedirects { get; set; }
        public string ContentType { get; set; }
        public string CharSet { get; set; }
        public string WrapperNode { get; set; }
        public List<ParameterSettings> RequestParameters { get; set; }
        public List<ParameterSettings> Headers { get; set; }
        public string TransformationData { get; set; }
        public List<ValidationSettings> ValidationSettings { get; set; }
        public DateTimeOffset? LastSuccessFetch { get; set; }
        public DateTimeOffset? LastSuccessParse { get; set; }

        public APISettings() {
            //OfficialDomain = string.Empty;
            RequestParameters = new List<ParameterSettings>();
            Headers = new List<ParameterSettings>();
            Verb = "GET";
            RequireHttps = false;
            FollowRedirects = false;
            ValidationSettings = new List<ValidationSettings>();
            LastSuccessFetch = DateTimeOffset.MinValue;
            LastSuccessParse = DateTimeOffset.MinValue;
        }

        public static APISettings GetAPISettings(string officialDomain, string baseDomain, string method, string verb) {
            APFyEntities db = new APFyEntities();
            APISettings settings = null;

            //var method = db.Method.Include("Api.Verb.RequestParameter.ParameterType.OutputTransformation.OutputValidation").Where(m=> (!string.IsNullOrEmpty(officialDomain) && ));

            var apiMethod = db.Method.Include("RequestParameter").Include("OutputTransformation").Include("OutputValidation").Include("MethodMeta").Include("Verb")
                .FirstOrDefault(m => ((!string.IsNullOrEmpty(officialDomain) && m.Api.OfficialDomain == officialDomain) || (!string.IsNullOrEmpty(baseDomain) && m.Api.DomainBase == baseDomain)) && ((m.Api.Namespace==null?"":m.Api.Namespace + "/") + m.APIPath) == method );

            //var apiSettings = (from a in db.Api
            //                   join m in db.Method.Include("Verb").Include("RequestParameter").Include("OutputTransformation").Include("OutputValidation").Include("MethodMeta") on a.Id equals m.Api_Id
            //                  where ((!string.IsNullOrEmpty(officialDomain) && a.OfficialDomain == officialDomain) || (!string.IsNullOrEmpty(baseDomain) && a.DomainBase == baseDomain)) && ((a.Namespace==null?"":a.Namespace + "/") + m.APIPath) == method
            //                  select new {Method = m, Api = a}).FirstOrDefault();

            if (apiMethod != null)
                settings = new APISettings { Id = apiMethod.Id, BaseUrl = apiMethod.OriginalPath, Verb = apiMethod.Verb.Name, RequireHttps = apiMethod.RequireHttps, FollowRedirects = apiMethod.FollowRedirect, RequestParameters = apiMethod.RequestParameter.Where(rp => rp.ParameterType_Id == 1).Select(rp => new ParameterSettings() { Key = rp.OriginalKey, MappedKey = rp.MappedKey, Required = rp.Required, ValidationRegExp = rp.ValidationRegexp, IsFile = rp.IsFile }).ToList(), Headers = apiMethod.RequestParameter.Where(rp => rp.ParameterType_Id == 2).Select(rp => new ParameterSettings() { Key = rp.OriginalKey, MappedKey = rp.MappedKey, Required = rp.Required, ValidationRegExp = rp.ValidationRegexp }).ToList(), TransformationData = apiMethod.OutputTransformation.Data, ValidationSettings = apiMethod.OutputValidation.Select(v => new ValidationSettings() { Namespace = v.Namespace, Data = v.Data??v.Path }).ToList(), LastSuccessFetch = apiMethod.MethodMeta == null ? null : apiMethod.MethodMeta.LastSuccessFetch, LastSuccessParse = apiMethod.MethodMeta == null ? null : apiMethod.MethodMeta.LastSuccessParse, ContentType=apiMethod.CustomContentType, CharSet=apiMethod.CustomCharSet, WrapperNode=apiMethod.WrapperNode };

            return settings;

            //if (apiSettings != null)
            //{
            //    settings.BaseUrl = apiSettings.BaseUrl;
            //    settings.Headers = apiSettings.
            //}
            //else
            //    return null;

            //Todo: get settings from database

            //if (baseDomain == "jquery.com" && method == "LastUpdated") {
            //    settings.BaseUrl = "http://jquery.com";
            //    settings.TransformationId = new Guid("80A21A44-A33E-11E1-A3C0-E3186188709B");//HttpContext.Current.Server.MapPath("~/Content/xslt/testJQuery.xslt");
            //    settings.ValidationSettings.Add(new ValidationSettings() { Namespace = "", Path = "testJQuery" });
            //}
            //else if (baseDomain == "www.myrepublica.com" && method == "GetSchedule") {
            //    settings.BaseUrl = "http://www.myrepublica.com/portal/index.php?action=pages";
            //    settings.TransformationId = new Guid("4F8CAE30-A341-11E1-B68F-2D1B6188709B");
            //    settings.RequestParameters.Add(new ParameterSettings() { Key="p", Required=true, ValidationRegExp=@"\d+", MappedKey="page_id"});
            //    settings.ValidationSettings.Add(new ValidationSettings() { Namespace = "", Path = "testMyRepublica" });
            //}

            //return settings;
        }

        /*
        public void SetLastSuccessDates(DateTimeOffset lastSuccessFetch, DateTimeOffset lastSuccessParse) {
            APFyEntities db = new APFyEntities();

            var method = db.Method.Single(m=>m.Id==this.Id);

            if (lastSuccessFetch != DateTimeOffset.MinValue)
                method.LastSuccessFetch = lastSuccessFetch;

            if (lastSuccessParse != DateTimeOffset.MinValue)
                method.LastSuccessParse = lastSuccessParse;

            db.SaveChanges();
        }        
        */

        /*
        public string GetBaseUrl(HttpRequest request) {
            string tmpUrl = BaseUrl;
            string pattern = @"\[(?<key>[^\]]+)\]";

            var matches = Regex.Matches(tmpUrl, pattern);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    string key = match.Groups["key"].Value;
                    tmpUrl = Regex.Replace(tmpUrl, string.Format(@"\[{0}\]", key), request.QueryString[key]);
                }
            }

            return tmpUrl;
        }

        public NameValueCollection PrepareRequestParameters(HttpRequest request) {
            NameValueCollection requestParams = new NameValueCollection();

            var inputParams = request.HttpMethod.Equals("GET", StringComparison.CurrentCultureIgnoreCase)?request.QueryString:request.Form;

            if (RequestParameters.Count > 0) {
                foreach (var paramSetting in RequestParameters) 
                    requestParams.Add(paramSetting.MappedKey??paramSetting.Key, inputParams[paramSetting.Key]);
            }

            return requestParams;
        }

        public NameValueCollection PrepareRequestHeaders(HttpRequest request)
        {
            NameValueCollection headers = new NameValueCollection();

            var inputHeaders = request.Headers;

            if (Headers.Count > 0)
                foreach (var paramSetting in Headers)
                    headers.Add(paramSetting.Key, inputHeaders[string.Format("X-RequestHeader-{0}", paramSetting.Key)]);

            return headers;
        }        
    
    */
    }
}