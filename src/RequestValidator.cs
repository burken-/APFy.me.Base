using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;

namespace APFy.me.utilities
{
    public class RequestValidator
    {
        /// <summary>
        /// Make sure the request contains all necessary data and that it is in the correct format
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="statusCode"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static ErrorCode ValidateRequest(HttpRequest request, out APISettings settings, out List<KeyValuePair<string, string>> errorList)
        {
            ErrorCode validationError;
            errorList = null;
            settings = null;

            //Validate api-key
            string apiKey = request.Headers[ConfigurationManager.AppSettings["api.parameterPrefix"] + "authorization"];

            validationError = ValidateApiKey(apiKey);
            if (validationError != ErrorCode.NoError)
                return validationError;

            validationError = ValidateUrl(apiKey, request, out settings);

            if (validationError != ErrorCode.NoError || settings == null)
                return validationError;

            validationError = ValidateRequestParameters(request, settings, out errorList);

            return validationError;
        }

        private static ErrorCode ValidateApiKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return ErrorCode.ApiKeyMissing;

            Guid apiKey;
            if (!Guid.TryParse(key, out apiKey))
                return ErrorCode.ApiKeyMalformed;

            APFyEntities db = new APFyEntities();
            var usr = db.User.Where(u => u.APIKey == apiKey).FirstOrDefault();

            if (usr == null)
                return ErrorCode.ApiKeyNonExisting;
            else
                return ErrorCode.NoError;
        }

        private static ErrorCode ValidateUrl(string key, HttpRequest request, out APISettings settings) {
            settings = null;
            string pattern;
            string officialDomain = string.Empty;
            string baseDomain = string.Empty;
            string path = string.Empty;

            if (request.Url.Host == ConfigurationManager.AppSettings["api.domain"])
                pattern = @"/(?<domain>[^/]+)/(?<method>.{5,})";
            else
            {
                pattern = @"/(?<method>.{5,})";
                officialDomain = request.Url.Host;
            }

            var pathMatch = Regex.Match(request.Path, pattern);

            if (!pathMatch.Success)
                return ErrorCode.UrlBadSyntax;

            //Validate if api exists
            if(string.IsNullOrEmpty(officialDomain))
                baseDomain = pathMatch.Groups["domain"].Value;
            path = pathMatch.Groups["method"].Value;

            string cacheKey = string.Format("{0}{1}", key, string.IsNullOrWhiteSpace(officialDomain)?baseDomain:officialDomain);
            if (HttpRuntime.Cache[cacheKey] is bool)
                return ErrorCode.ApiKeyRequestLimitReached;

            HttpRuntime.Cache.Insert(cacheKey, true, null, Cache.NoAbsoluteExpiration, TimeSpan.FromSeconds(int.Parse(ConfigurationManager.AppSettings["api.requestLimit"])), CacheItemPriority.NotRemovable, null);

            //Get the API-settings
            settings = APISettings.GetAPISettings(officialDomain, baseDomain, path, request.HttpMethod);

            if (settings == null)
                return ErrorCode.UrlApiNotFound;

            if (!request.IsSecureConnection && settings.RequireHttps)
                return ErrorCode.UrlHttpsRequired;

            return ErrorCode.NoError;
        }

        private static ErrorCode ValidateRequestParameters(HttpRequest request, APISettings settings, out List<KeyValuePair<string, string>> errorList)
        {
            errorList = new List<KeyValuePair<string, string>>();

            //Validate method
            if (!request.HttpMethod.Equals(settings.Verb, StringComparison.CurrentCultureIgnoreCase))
                return ErrorCode.RequestBadMethod;

            if (settings.RequestParameters.Count > 0) {
                var reqParams = settings.Verb.Equals("GET", StringComparison.CurrentCultureIgnoreCase) ? request.QueryString : request.Form;

                foreach (var paramSettings in settings.RequestParameters) {
                    string key = paramSettings.MappedKey ?? paramSettings.Key;
                    object value;

                    if (paramSettings.IsFile)
                        value = request.Files[key];
                    else
                        value = reqParams[key];

                    if (paramSettings.Required && value == null)
                        errorList.Add(new KeyValuePair<string, string>(key, string.Format("Required parameter \"{0}\" is missing", key)));

                    if (value != null && !string.IsNullOrEmpty(paramSettings.ValidationRegExp) && !Regex.IsMatch( (paramSettings.IsFile?((HttpPostedFile)value).FileName: (value as string) ?? ""), paramSettings.ValidationRegExp))
                        errorList.Add(new KeyValuePair<string,string>(key, string.Format("Value of parameter: \"{0}\" must follow the pattern: {1}", key, paramSettings.ValidationRegExp)));
                }

                if (errorList.Count != 0)
                    return ErrorCode.RequestInvalidParameters;
            }

            if (settings.Headers.Count > 0)
            {
                foreach (var paramSettings in settings.Headers)
                {
                    string key = paramSettings.MappedKey ?? paramSettings.Key;
                    string value = request.Headers[key];
                    if (paramSettings.Required && value == null)
                        errorList.Add(new KeyValuePair<string, string>(key, string.Format("Required header \"{0}\" is missing", key)));

                    if (value != null && !string.IsNullOrEmpty(paramSettings.ValidationRegExp) && !Regex.IsMatch(value ?? "", paramSettings.ValidationRegExp))
                        errorList.Add(new KeyValuePair<string, string>(key, string.Format("Value of header: \"{0}\" must follow the pattern: {1}", key, paramSettings.ValidationRegExp)));
                }

                if (errorList.Count != 0)
                    return ErrorCode.RequestInvalidHeaders;
            }

            return ErrorCode.NoError;
        }
    }
}