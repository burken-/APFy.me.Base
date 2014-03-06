using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Xml;
using System;

namespace MyAPI.Utilities
{
    public class XmlToJsonStream_org:Stream
    {
        private readonly Stream _stream;
        private long _position;

        public XmlToJsonStream_org(Stream s) {
            _stream = s;
        }

        public override bool CanRead
        {
            get { return this._stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return this._stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return this._stream.CanWrite; }
        }

        public override void Flush()
        {
            this._stream.Flush();
        }

        public override long Length
        {
            get { return this._stream.Length; }
        }

        public override long Position
        {
            get
            {
                return this._position;
            }
            set
            {
                this._position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this._stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this._stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this._stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MemoryStream ms = new MemoryStream(buffer);

            //string b = Encoding.UTF8.GetString(buffer, offset, count);
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(ms);
                //doc.LoadXml(string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>{0}",b));
                string json = JsonConvert.SerializeXmlNode(doc);
                byte[] data = Encoding.UTF8.GetBytes(json);
                this._stream.Write(data, 0, data.Length);
            }
            catch (Exception e) { 
            
            }
        }
    }
}