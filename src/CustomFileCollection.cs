using System;
using System.Collections;
using System.Web;
using System.IO;

namespace APFy.me.utilities
{
    public class CustomFileCollection : HttpFileCollectionBase
    {
        public CustomFileCollection() { }
        
        public CustomFileCollection(IDictionary d, bool readOnly)
        {
            foreach (DictionaryEntry e in d)
                this.BaseAdd((string)e.Key, e.Value);
        }

        public override HttpPostedFileBase this[int index]
        {
            get { return (HttpPostedFileBase)this.BaseGet(index); }
        }

        public override HttpPostedFileBase this[string key]
        {
            get
            {
                return (HttpPostedFileBase)this.BaseGet(key);
            }
        }

        public override int Count {
            get { return this.BaseGetAllKeys().Length; }
        }

        // Gets a String array that contains all the keys in the collection. 
        public override String[] AllKeys
        {
            get
            {
                return (this.BaseGetAllKeys());
            }
        }

        // Gets an Object array that contains all the values in the collection. 
        public Array AllValues
        {
            get
            {
                return (this.BaseGetAllValues());
            }
        }

        // Gets a String array that contains all the values in the collection. 
        public String[] AllStringValues
        {
            get
            {
                return ((String[])this.BaseGetAllValues(typeof(string)));
            }
        }

        // Gets a value indicating if the collection contains keys that are not null. 
        public Boolean HasKeys
        {
            get
            {
                return (this.BaseHasKeys());
            }
        }

        // Adds an entry to the collection. 
        public void Add(String key, Object value)
        {
            this.BaseAdd(key, value);
        }

        // Removes an entry with the specified key from the collection. 
        public void Remove(String key)
        {
            this.BaseRemove(key);
        }

        // Removes an entry in the specified index from the collection. 
        public void Remove(int index)
        {
            this.BaseRemoveAt(index);
        }

        // Clears all the elements in the collection. 
        public void Clear()
        {
            this.BaseClear();
        }
    }
}
