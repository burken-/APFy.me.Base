using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Xsl;
using Mvp.Xml.Common.Xsl;

namespace APFy.me.utilities
{
    public class XmlHelper
    {
        public static XmlReaderSettings DefaultReaderSettings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Ignore, IgnoreProcessingInstructions=true, IgnoreWhitespace=true, CloseInput=true, XmlResolver = null, ConformanceLevel=ConformanceLevel.Auto};

        public static XmlReader Transform(XmlReader input, XmlReader xsltBase, Dictionary<string,string> inputArgs, out ErrorCode errorCode, out string errorString, out XmlWriterSettings xsltOutputSettings, string cacheKey)
        {
            errorCode = ErrorCode.NoError;
            errorString = string.Empty;
            
            MvpXslTransform xslt = null;
            
            if(!string.IsNullOrEmpty(cacheKey))
                xslt = (MvpXslTransform)System.Web.HttpRuntime.Cache.Get(cacheKey);

            XsltArgumentList args = new XsltArgumentList();
            //Remove all default extension objects
            args.Clear();

            if(inputArgs != null)
                foreach (var arg in inputArgs)
                    args.AddParam(arg.Key, string.Empty, arg.Value);

            xsltOutputSettings = null;
            XmlReader output;
            try
            {
                if (xslt == null)
                {
#if DEBUG
                    xslt = new MvpXslTransform(true);
#else
                    xslt = new MvpXslTransform();
#endif

                    xslt.Load(xsltBase);

                    if(!string.IsNullOrEmpty(cacheKey))
                        System.Web.HttpRuntime.Cache.Insert(cacheKey, xslt);
                }

                output = xslt.Transform(new XmlInput(input), args);
                xsltOutputSettings = xslt.OutputSettings;
                //xsltOutputSettings.Indent = false;
                //xsltOutputSettings.CloseOutput = true;
                //MemoryStream ms = new MemoryStream();
                //XmlWriter w = XmlWriter.Create(ms);
                //xslt.Transform(input, w);

                //ms.Position = 0;
                //output = XmlReader.Create(ms);
            }
            catch (Exception ex)
            {
                //Nothing
                output = null;
                errorCode = ErrorCode.XsltLoadFail;
                errorString = ex.Message;
            }
            finally
            {
                xsltBase.Close();
            }

            return output;
        }

        public static XmlReader ValidateXml(XmlReader reader, List<ValidationSettings> validationSettings, System.Xml.Schema.ValidationEventHandler validationHandler, out ErrorCode errorCode, out string errorString)
        {
            errorCode = ErrorCode.NoError;
            errorString = string.Empty;


            XmlReaderSettings settings = XmlHelper.DefaultReaderSettings.Clone(); // new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            //settings.DtdProcessing = DtdProcessing.Ignore;
            //settings.XmlResolver = null;

            try
            {
                foreach (var schema in validationSettings)
                {
                    if (schema.Data.EndsWith(".xsd") || schema.Data.StartsWith("http"))
                    {
                        settings.Schemas.Add(schema.Namespace, schema.Data);
                    }
                    else if(!string.IsNullOrEmpty(schema.Data.Trim()))
                    {
                        XmlTextReader xsd = new XmlTextReader(new StringReader(schema.Data));
                        settings.Schemas.Add(schema.Namespace, xsd);
                    }
                }
            }
            catch (Exception e)
            {
                errorCode = ErrorCode.XsdLoadFail;
                errorString = e.ToString();
            }

            if (validationHandler != null)
            {
                settings.ValidationEventHandler += validationHandler;
                settings.Schemas.ValidationEventHandler += validationHandler;
            }

            return XmlReader.Create(reader, settings);
        }

        public static bool IsValidXslt(string xml, out string errorMessage) {

            //if (IsValidXml(xml))
            //{
            //    XmlDocument xd = new XmlDocument();
            //    xd.LoadXml(xml);

            //    xd.Schemas.Add("http://www.w3.org/1999/XSL/Transform", System.IO.Path.Combine(System.Web.HttpRuntime.AppDomainAppPath, "App_Data\\xslt.xsd"));
            //    xd.Schemas.Compile();

            //    StringBuilder validationError = new StringBuilder();
            //    ValidationEventHandler validationHandler = delegate(object sender, ValidationEventArgs e)
            //    {
            //        validationError.Append(string.Format("Validation {0}: {1}", e.Severity, e.Message));
            //    };
            //    // new ValidationEventHandler(ValidationEventHandler);
            //    xd.Validate(validationHandler);

            //    return validationError.Length == 0;
            //}
            //else {
            //    return false;
            //}

            XmlDocument xd = new XmlDocument();
            var xslt = new System.Xml.Xsl.XslCompiledTransform();
            //Load the xslt to make sure the output settings are correct
            try
            {
                xd.LoadXml(xml);
                xslt.Load(xd);

                if (!(xslt.OutputSettings.OutputMethod == XmlOutputMethod.Xml && xslt.OutputSettings.OmitXmlDeclaration && xslt.OutputSettings.Encoding == Encoding.UTF8))
                {
                    errorMessage = "The XSLT needs to have the output method set to XML, OmitXmlDeclaration set to true and encoding set to utf-8";
                    return false;
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool IsValidXsd(string xml, out string errorMessage) {
            //try
            //{
            //    StringReader sr = new StringReader(xml);
            //    XmlSchema xs = XmlSchema.Read(sr, null);
            //    xs.Compile(null);
            //}
            //catch (Exception e)
            //{
            //    return false;
            //}

            //return true;
            //Validate the xsd against the xsd-schema
            if (IsValidXml(xml, out errorMessage))
            {
                XmlDocument xd = new XmlDocument();
                xd.LoadXml(xml);
                xd.Schemas.Add("http://www.w3.org/2001/XMLSchema", System.IO.Path.Combine(System.Web.HttpRuntime.AppDomainAppPath, "App_Data\\xsdschema.xsd"));
                xd.Schemas.Compile();

                StringBuilder validationError = new StringBuilder();
                ValidationEventHandler validationHandler = delegate(object sender, ValidationEventArgs e) {
                    validationError.Append(string.Format("Validation {0}: {1}",e.Severity, e.Message));
                };
                    // new ValidationEventHandler(ValidationEventHandler);
                xd.Validate(validationHandler);

                errorMessage = validationError.ToString();
                return validationError.Length == 0;
            }
            else {
                return false;
            }
            

            //xd.Schemas.Add("http://www.w3.org/2001/XMLSchema", System.IO.Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data\\xsdschema.xsd"));
            //xd.Schemas.Compile();

            //ValidationEventHandler validationHandler = new ValidationEventHandler(ValidationEventHandler);
            //xd.Validate(validationHandler);

            //if (validationError.Length > 0)
            //    return Content("false");
        }

        public static bool IsValidXml(string xml, out string errorMessage) {
            XmlDocument xd = new XmlDocument();

            //Make sure it's a valid xml file
            try
            {
                xd.LoadXml(xml);
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static XmlSchemaSet InferXsd(string xml) {
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            XmlSchemaInference infer = new XmlSchemaInference();
            StringReader sr = new StringReader(xml);
            XmlReader r = XmlReader.Create(sr);
            schemaSet = infer.InferSchema(r);

            return schemaSet;
        }
    }
}
