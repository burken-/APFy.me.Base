using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Xml;

namespace MyAPI.Utilities
{
    public class TextToXmlStream:Stream
    {
        private readonly Stream _stream;
        private long _position;
        private string _rootElement;
        private bool _asBase64;
        private Dictionary<string, string> _attributes;

        public TextToXmlStream(Stream s, string rootElement, bool asBase64, Dictionary<string,string> attributes) {
            _stream = s;
            _rootElement = rootElement;
            _asBase64 = asBase64;
            _attributes = attributes;
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
            XmlWriter writer = XmlWriter.Create(this._stream);
            writer.WriteStartElement(_rootElement);
            foreach (string key in _attributes.Keys) 
                writer.WriteAttributeString(key, _attributes[key]);

            if (_asBase64)
                writer.WriteBase64(buffer, offset, count);
            else
                writer.WriteCData(Encoding.UTF8.GetString(buffer, offset, count));

            writer.WriteEndElement();
        }
    }
}