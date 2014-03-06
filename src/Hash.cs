using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace APFy.me.utilities
{
    public class Hash
    {
        public Hash() { }

        public enum HashType : int
        {
            MD5,
            SHA1,
            SHA256,
            SHA512,
            HMACSHA256,
            HMACSHA1
        }

        public static bool CheckHash(string original, string hashString, HashType hashType)
        {
            return (GetHash(original, hashType) == hashString);
        }

        public static string GetHash(string text, string key, HashType hashType)
        {
            return GetHash(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes(key), hashType);
        }

        public static string GetHash(string text, HashType hashType)
        {
            return GetHash(Encoding.UTF8.GetBytes(text), hashType);
        }

        public static string GetHash(byte[] bytes, byte[] keyBytes, HashType hashType)
        {
            byte[] hashValue = GetHashBytes(bytes, keyBytes, hashType);

            return BytesToHexString(hashValue);
        }

        public static string GetHash(byte[] bytes, HashType hashType)
        {
            return GetHash(bytes, null, hashType);
        }

        public static byte[] GetHashBytes(string text, string key, HashType hashType)
        {
            return GetHashBytes(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes(key), hashType);
        }

        public static byte[] GetHashBytes(byte[] bytes, HashType hashType)
        {
            return GetHashBytes(bytes, null, hashType);
        }

        public static byte[] GetHashBytes(byte[] bytes, byte[] keyBytes, HashType hashType)
        {
            HashAlgorithm hash;

            switch (hashType)
            {
                case HashType.MD5:
                    hash = new MD5CryptoServiceProvider();
                    break;
                case HashType.SHA1:
                    hash = new SHA1CryptoServiceProvider();
                    break;
                case HashType.SHA256:
                    hash = new SHA256CryptoServiceProvider();
                    break;
                case HashType.SHA512:
                    hash = new SHA512CryptoServiceProvider();
                    break;
                case HashType.HMACSHA256:
                    hash = new HMACSHA256(keyBytes);
                    break;
                case HashType.HMACSHA1:
                    hash = new HMACSHA1(keyBytes);
                    break;
                default:
                    hash = new SHA1CryptoServiceProvider();
                    break;
            }

            byte[] hashValue;
            hashValue = hash.ComputeHash(bytes);
            hash.Dispose();

            return hashValue;
        }

        public static string CreateSaltString(int size)
        {
            return System.Convert.ToBase64String(CreateSalt(size));
        }

        public static byte[] CreateSalt(int size) {
            // Generate a cryptographic random number using the cryptographic 
            // service provider
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[size];
            rng.GetBytes(buff);

            return buff;
        }

        public static string BytesToHexString(byte[] input) {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in input)
                sb.Append(string.Format("{0:x2}", b));

            return sb.ToString();           
        }

        public static string BytesToBase64(byte[] input) {
            return System.Convert.ToBase64String(input);
        }

        public static byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 == 1)
                return null;

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static bool ArrayMatch(IStructuralEquatable arr1, IStructuralEquatable arr2)
        {
            return arr1.Equals(arr2, StructuralComparisons.StructuralEqualityComparer);
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}