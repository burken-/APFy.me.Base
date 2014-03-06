using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Web;
using System.Xml;
using NLog;

namespace APFy.me.utilities
{
    public class APFyHandler : IHttpHandler
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public bool IsReusable
        {
            get { return false; }
        }

        private HttpContext _ctx;
        private List<KeyValuePair<string, string>> validationErrors;

        private bool _debug;
        private string _parameterprefix;

        public void ProcessRequest(HttpContext context)
        {
            _ctx = context;

            _parameterprefix = ConfigurationManager.AppSettings["api.parameterPrefix"];
            _debug = (context.Request.Headers[_parameterprefix + "debug"]??"").Equals("true", StringComparison.CurrentCultureIgnoreCase);

            //Log request to file
            if (_debug)
                RequestHelper.LogRequest(_ctx.Request);

            //Prepares the necessary filters for the output
            SetOuputSettings();

            ErrorCode errorCode = ErrorCode.NoError;
            APISettings settings;
            XmlReader output = null;
            XmlWriterSettings outputSettings = new XmlWriterSettings();
            outputSettings.Indent = false;
            outputSettings.CloseOutput = true;

            List<KeyValuePair<string, string>> errorList = new List<KeyValuePair<string, string>>();

            //Validate the request
            errorCode = RequestValidator.ValidateRequest(_ctx.Request, out settings, out errorList);

            if (errorCode != ErrorCode.NoError)
            {
                SendResultToBrowser(output, null, settings, errorCode, errorList, outputSettings);
                return;
            }

            //Make the request to the underlying server
            HttpWebRequest req;
            try
            {
                req = RequestHelper.PrepareRequest(settings, _ctx.Request);
            }
            catch (Exception e) {
                SendResultToBrowser(output, null, settings, ErrorCode.RequestFailed, errorList, outputSettings);
                logger.WarnException("Problem connecting to the source server", e);
                return;
            }

            HttpWebResponse res;
            Exception exception;
            res = ResponseHelper.PrepareResponse(req, out exception);

            if (res == null) {
                SendResultToBrowser(output, null, settings, ErrorCode.RequestBadResponse, errorList, outputSettings);
                return;
            }
            else if (res.StatusCode != HttpStatusCode.OK)
            {
                output = ContentConverter.ConvertContent(res.GetResponseStream(), res.ContentType, res.CharacterSet, null);
                ResponseHelper.SetResponseHeaders(res, _ctx.Response);
                ResponseHelper.SetResponseCookies(res, _ctx.Response, _ctx.Request);
                SendResultToBrowser(output, res, settings, ErrorCode.RequestBadResponse, errorList, outputSettings);
                return;
            }
            else { 
                //Handle the real response and add transformation and validations
                Stream inputStream = res.GetResponseStream();
                if (!string.IsNullOrWhiteSpace(settings.WrapperNode))
                    inputStream = new Mvp.Xml.Common.XmlFragmentStream(inputStream, settings.WrapperNode.Trim());

                output = ContentConverter.ConvertContent(inputStream, string.IsNullOrWhiteSpace(settings.ContentType) ? res.ContentType : settings.ContentType, string.IsNullOrWhiteSpace(settings.CharSet) ? res.CharacterSet : settings.CharSet, settings.WrapperNode);

                ResponseHelper.SetResponseHeaders(res, _ctx.Response);
                ResponseHelper.SetResponseCookies(res, _ctx.Response, _ctx.Request);

                //Transform the output
                if (!string.IsNullOrWhiteSpace(settings.TransformationData)) {
                    Dictionary<string, string> inputArgs = new Dictionary<string, string>();
                    foreach(string header in _ctx.Request.Headers.Keys){
                        if (header.StartsWith(string.Format("{0}param-",_parameterprefix)))
                            inputArgs.Add(header.Replace(string.Format("{0}param-", _parameterprefix), string.Empty), _ctx.Request.Headers[header]);
                    }

                    output = TransformWithXslt(output, settings.TransformationData, inputArgs, out errorCode, out outputSettings, string.Concat("xslt_",settings.Id));

                    if (errorCode != ErrorCode.NoError) {
                        SendResultToBrowser(output, res, settings, errorCode, errorList, outputSettings);
                        return;
                    }
                }

                //Todo: Implement validation frequency so validation is not triggered on every call
                if (settings.ValidationSettings.Count > 0) 
                    output = ValidateXml(output, settings.ValidationSettings, out errorCode);

                SendResultToBrowser(output, res, settings, errorCode, errorList, outputSettings);
            }
        }

        /// <summary>
        /// Add the correct response filters depending on the accept headers
        /// </summary>
        private void SetOuputSettings()
        {
            if (string.Format(",{0},", (_ctx.Request.Headers[_parameterprefix + "accept"] ?? "").ToLower()).Contains(",application/json,"))
            {
                ResponseFilterStream rs = new ResponseFilterStream(_ctx.Response.Filter, _ctx.Response.ContentEncoding);
                _ctx.Response.ContentType = string.Format("application/json; charset={0}", _ctx.Response.Charset);
                rs.TransformString += ContentConverter.ConvertXmlToJson;
                _ctx.Response.Filter = rs;
            }
            else
                _ctx.Response.ContentType = string.Format("text/xml; charset={0}", _ctx.Response.Charset);
        }

        private XmlReader TransformWithXslt(XmlReader input, string xsltData, Dictionary<string, string> inputArgs, out ErrorCode errorCode, out XmlWriterSettings outputSettings, string cacheKey)
        {
            errorCode = ErrorCode.NoError;
            string errorString;

            //XmlReader xsltReader = new XmlTextReader(_ctx.Server.MapPath(string.Format("~/App_Data/XmlTransformation/{0}.xslt", xsltPath)));
            XmlReader xsltReader = XmlReader.Create(new StringReader(xsltData), XmlHelper.DefaultReaderSettings);

            return XmlHelper.Transform(input, xsltReader, inputArgs, out errorCode, out errorString, out outputSettings, cacheKey);
        }

        private XmlReader ValidateXml(XmlReader reader, List<ValidationSettings> validationSettings, out ErrorCode errorCode) {
            var handler = new System.Xml.Schema.ValidationEventHandler(settings_ValidationEventHandler);

            //foreach(var settings in validationSettings)
            //    if(!settings.Data.StartsWith("http"))
            //        settings.Data = _ctx.Server.MapPath(string.Format("~/App_Data/XmlValidation/{0}.xsd", settings.Data));

            string errorString;
            return XmlHelper.ValidateXml(reader, validationSettings, handler, out errorCode, out errorString);
        }

        private void settings_ValidationEventHandler(object sender, System.Xml.Schema.ValidationEventArgs e)
        {
            if (validationErrors == null)
                validationErrors = new List<KeyValuePair<string, string>>();

            validationErrors.Add(new KeyValuePair<string, string>(string.Format("Xml validation error, severity: {0}", e.Severity), e.Message));
        }

        /// <summary>
        /// Send the final response to the browser
        /// </summary>
        /// <param name="output">The base xml that will be transformed and validated</param>
        /// <param name="baseResponse">The base response from the underlying server. If we get any other status code than 200 we append the response value to the result.</param>
        /// <param name="errorCode">Any error code encountered on the way</param>
        /// <param name="errorList">Additional information about eventual errors</param>
        private void SendResultToBrowser(XmlReader output, HttpWebResponse baseResponse, APISettings settings, ErrorCode error, List<KeyValuePair<string, string>> errorList, XmlWriterSettings outputSettings) {
            
            //outputSettings.OmitXmlDeclaration = true;
            //outputSettings.Encoding = System.Text.Encoding.UTF8;
            ErrorCode baseError = error;
            XmlWriter outputWriter = XmlWriter.Create(_ctx.Response.OutputStream, outputSettings); //new XmlTextWriter(_ctx.Response.OutputStream, System.Text.Encoding.UTF8);

            if(errorList == null)
                errorList = new List<KeyValuePair<string, string>>();

            if(_debug)
                outputWriter.WriteStartElement("Result");

            if (baseResponse != null) { 
                //Write the origin-tag
                _ctx.Response.AddHeader(_parameterprefix + "origin", baseResponse.ResponseUri.ToString());
                //outputWriter.WriteElementString("Origin", baseResponse.ResponseUri.ToString());

                if (baseResponse.StatusCode != HttpStatusCode.OK)
                    _ctx.Response.StatusCode = (int)baseResponse.StatusCode;

                //Close the base response
                baseResponse.Close();
            }else
                //If we didn't get any response it was a bad request
                _ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            if (output != null && error == ErrorCode.NoError)
            {
                //Write the output
                if (_debug)
                {
                    //outputWriter.WriteStartElement("BaseResponse");
                    //outputWriter.WriteCData("");
                    //outputWriter.WriteEndElement(); //BaseResponse
                    outputWriter.WriteStartElement("ProcessedData");
                }

                try
                {
                    outputWriter.WriteNode(output, false);
                }
                catch (Exception e)
                {
                    error = ErrorCode.UnknownError;
                    errorList.Add(new KeyValuePair<string, string>("Problem with output", e.ToString()));
                    logger.WarnException("Problem sending response to the client", e);
                }

                if(_debug)
                    outputWriter.WriteEndElement(); //Data
            }

            if (validationErrors != null && validationErrors.Count > 0) {
                error = ErrorCode.ValidationFailed;
                errorList.AddRange(validationErrors);
            }

            if (baseError != ErrorCode.NoError || (error != ErrorCode.NoError && _debug))
            {
                //Write status nodes
                outputWriter.WriteStartElement("APFyError");
                outputWriter.WriteAttributeString("Code", ((int)error).ToString());

                DateTimeOffset? lastFetch = DateTimeOffset.Now;
                DateTimeOffset? lastParse = DateTimeOffset.Now;
                
                if (settings == null)
                {
                    lastFetch = null;
                    lastParse = null;
                }
                else {
                    if ((int)error < 400)
                        lastFetch = settings.LastSuccessFetch.HasValue ? settings.LastSuccessFetch : null;
                    else
                        lastParse = settings.LastSuccessParse.HasValue ? settings.LastSuccessParse : null;
                }

                outputWriter.WriteAttributeString("LastFetchSuccess", lastFetch.HasValue?lastFetch.Value.ToString():"");
                outputWriter.WriteAttributeString("LastParseSuccess", lastParse.HasValue ? lastParse.Value.ToString() : "");

                if (_debug && errorList.Count > 0)
                {
                    foreach (var val in errorList)
                    {
                        outputWriter.WriteStartElement("Error");
                        outputWriter.WriteAttributeString("Type", val.Key);
                        outputWriter.WriteCData(val.Value);
                        outputWriter.WriteEndElement(); //Error
                    }
                }

                outputWriter.WriteEndElement(); //Error
            }

            if(_debug)
                outputWriter.WriteEndElement(); //Result

            //Close readers and writers
            outputWriter.Close();

            _ctx.Response.AddHeader(_parameterprefix + "status", ((int)error).ToString());

            if(output != null)
                output.Close();

            if (settings != null)
            {
                Logger usageLogger = LogManager.GetLogger("UsageLog");
                LogEventInfo usageLog = new LogEventInfo(LogLevel.Info, "UsageLog", "");

                usageLog.Properties["MethodId"] = settings.Id;
                usageLog.Properties["APIKey"] = _ctx.Request.Headers[ConfigurationManager.AppSettings["api.parameterPrefix"] + "authorization"];
                usageLog.Properties["Status"] = (int)error;

                usageLogger.Log(usageLog);
            }
        }
    }
}