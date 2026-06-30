using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace Shared
{
    [Serializable]
    public class Poruka
    {
        public Igrac NaPotezu { get; set; }
        public Igrac Napadnut { get; set; }
        public TipPoruke tipPoruke { get; set; }
        public string poruka { get; set; }

        // AES-256 key (32 bytes)
        private static readonly byte[] key =
            Convert.FromBase64String("MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");

        // IV (16 bytes)
        private static readonly byte[] iv =
            Convert.FromBase64String("QUJDREVGR0hJSktMTU5PUA==");

        static Poruka()
        {
            if (key.Length != 32)
                throw new Exception("AES key mora biti 32 bajta (256-bit)");

            if (iv.Length != 16)
                throw new Exception("AES IV mora biti 16 bajta");
        }

        public Poruka(Igrac naPotezu, Igrac napadnut, TipPoruke tipPoruke, string poruka)
        {
            NaPotezu = naPotezu;
            Napadnut = napadnut;
            this.tipPoruke = tipPoruke;
            this.poruka = poruka;
        }

        public Poruka()
        {
        }

        public byte[] Serializuj()
        {
            BinaryFormatter bf = new BinaryFormatter();

            using (MemoryStream temp = new MemoryStream())
            {
                bf.Serialize(temp, this);
                byte[] podaci = temp.ToArray();

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (MemoryStream encrypted = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(
                            encrypted,
                            aes.CreateEncryptor(),
                            CryptoStreamMode.Write))
                        {
                            cs.Write(podaci, 0, podaci.Length);
                            cs.FlushFinalBlock();
                        }

                        return encrypted.ToArray();
                    }
                }
            }
        }

        public static Poruka DeserializujPoruku(byte[] bytes)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (MemoryStream msEncrypted = new MemoryStream(bytes))
                using (CryptoStream cs = new CryptoStream(
                    msEncrypted,
                    aes.CreateDecryptor(),
                    CryptoStreamMode.Read))
                using (MemoryStream msDecrypted = new MemoryStream())
                {
                    cs.CopyTo(msDecrypted);
                    msDecrypted.Position = 0;

                    BinaryFormatter bf = new BinaryFormatter();
                    return (Poruka)bf.Deserialize(msDecrypted);
                }
            }
        }
    }
}