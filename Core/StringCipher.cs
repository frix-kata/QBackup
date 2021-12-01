using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;



namespace QBackup
{

    public static class StringCipher
    {
        //https://stackoverflow.com/a/10177020/2061103
        //https://github.com/nopara73/DotNetEssentials/blob/master/DotNetEssentials/Crypto/StringCipher.cs
        //https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp
        //https://stackoverflow.com/a/10177020

        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int _keysize = 128;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int _derivation_iterations = 1000;

        public static string Encrypt(string plain_text, string pass_phrase)
        {
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var salt_string_bytes = Generate128BitsOfRandomEntropy();
            var iv_string_bytes = Generate128BitsOfRandomEntropy();
            var plain_text_bytes = Encoding.UTF8.GetBytes(plain_text);
            using (var password = new Rfc2898DeriveBytes(pass_phrase, salt_string_bytes, _derivation_iterations))
            {
                var key_bytes = password.GetBytes(_keysize / 8);
                using (var symmetric_key = new RijndaelManaged())
                {
                    symmetric_key.BlockSize = 128;
                    symmetric_key.Mode = CipherMode.CBC;
                    symmetric_key.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetric_key.CreateEncryptor(key_bytes, iv_string_bytes))
                    {
                        using (var memory_stream = new MemoryStream())
                        {
                            using (var crypto_stream = new CryptoStream(memory_stream, encryptor, CryptoStreamMode.Write))
                            {
                                crypto_stream.Write(plain_text_bytes, 0, plain_text_bytes.Length);
                                crypto_stream.FlushFinalBlock();
                                // Create the final bytes as a concatenation of the random salt bytes, the random iv bytes and the cipher bytes.
                                var cipher_text_bytes = salt_string_bytes;
                                cipher_text_bytes = cipher_text_bytes.Concat(iv_string_bytes).ToArray();
                                cipher_text_bytes = cipher_text_bytes.Concat(memory_stream.ToArray()).ToArray();
                                memory_stream.Close();
                                crypto_stream.Close();
                                return Convert.ToBase64String(cipher_text_bytes);
                            }
                        }
                    }
                }
            }
        }

        public static string Decrypt(string cipher_text, string pass_phrase)
        {
            // Get the complete stream of bytes that represent:
            // [32 bytes of Salt] + [16 bytes of IV] + [n bytes of CipherText]
            var cipher_text_bytes_with_salt_and_iv = Convert.FromBase64String(cipher_text);
            // Get the saltbytes by extracting the first 16 bytes from the supplied cipherText bytes.
            var salt_string_bytes = cipher_text_bytes_with_salt_and_iv.Take(_keysize / 8).ToArray();
            // Get the IV bytes by extracting the next 16 bytes from the supplied cipherText bytes.
            var iv_string_bytes = cipher_text_bytes_with_salt_and_iv.Skip(_keysize / 8).Take(_keysize / 8).ToArray();
            // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
            var cipher_text_bytes = cipher_text_bytes_with_salt_and_iv.Skip((_keysize / 8) * 2).Take(cipher_text_bytes_with_salt_and_iv.Length - ((_keysize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(pass_phrase, salt_string_bytes, _derivation_iterations))
            {
                var key_bytes = password.GetBytes(_keysize / 8);
                using (var symmetric_key = new RijndaelManaged())
                {
                    symmetric_key.BlockSize = 128;
                    symmetric_key.Mode = CipherMode.CBC;
                    symmetric_key.Padding = PaddingMode.PKCS7;
                    using (var decryptor = symmetric_key.CreateDecryptor(key_bytes, iv_string_bytes))
                    {
                        using (var memory_stream = new MemoryStream(cipher_text_bytes))
                        {
                            using (var crypto_stream = new CryptoStream(memory_stream, decryptor, CryptoStreamMode.Read))
                            {
                                var plain_text_bytes = new byte[cipher_text_bytes.Length];
                                var decrypted_byte_count = crypto_stream.Read(plain_text_bytes, 0, plain_text_bytes.Length);
                                memory_stream.Close();
                                crypto_stream.Close();
                                return Encoding.UTF8.GetString(plain_text_bytes, 0, decrypted_byte_count);
                            }
                        }
                    }
                }
            }
        }

        private static byte[] Generate128BitsOfRandomEntropy()
        {
            var random_bytes = new byte[16]; // 16 Bytes will give us 128 bits.
            using (var rng_csp = new RNGCryptoServiceProvider())
            {
                // Fill the array with cryptographically secure random bytes.
                rng_csp.GetBytes(random_bytes);
            }
            return random_bytes;
        }
    }

}