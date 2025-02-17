#nullable disable

using System.Collections.Generic;
using System.Security.Cryptography;
using AsarLib.Json;

namespace AsarLib
{
    public partial class Filesystem
    {
        public class FileIntegrity
        {
            private const int IntegrityBlockSize = 4 * 1024 * 1024;

            private static readonly SHA256 Sha256 = SHA256.Create();
            private static readonly object Lock = new object();

            public string Algorithm;

            public List<string> Blocks;

            public long BlockSize;

            public string Hash;

            public JsonWriter Serialize(JsonWriter writer)
            {
                writer.WriteSymbol(JsonSymbol.OpenCurly).WritePropertyKey("algorithm");
                if (Algorithm != null) writer.WriteString(Algorithm).WriteSymbol(JsonSymbol.Comma);
                else writer.WriteSymbol(JsonSymbol.Null).WriteSymbol(JsonSymbol.Comma);

                writer.WritePropertyKey("hash");
                if (Hash != null) writer.WriteString(Hash).WriteSymbol(JsonSymbol.Comma);
                else writer.WriteSymbol(JsonSymbol.Null).WriteSymbol(JsonSymbol.Comma);

                writer.WritePropertyKey("blockSize").WriteLong(BlockSize).WriteSymbol(JsonSymbol.Comma)
                    .WritePropertyKey("blocks").WriteSymbol(JsonSymbol.OpenSquare);
                foreach (var block in Blocks)
                    writer.WriteString(block).WriteSymbol(JsonSymbol.Comma);
                if (Blocks.Count > 0) writer.RemoveEndBytes(1);
                return writer.WriteSymbol(JsonSymbol.CloseSquare).WriteSymbol(JsonSymbol.CloseCurly);
            }

            public static FileIntegrity Deserialize(JsonReader reader)
            {
                var integrity = new FileIntegrity();
                while (true)
                    switch (reader.TakePropertyName())
                    {
                        case "algorithm":
                            integrity.Algorithm = reader.TakeString();
                            break;
                        case "hash":
                            integrity.Hash = reader.TakeString();
                            break;
                        case "blockSize":
                            integrity.BlockSize = (long)reader.TakeNumber();
                            break;
                        case "blocks":
                            integrity.Blocks = new List<string>();
                            while (reader.TakeNextArrayElement())
                                integrity.Blocks.Add(reader.TakeString());
                            break;
                        case null:
                            return integrity;
                        default:
                            reader.SkipToken();
                            break;
                    }
            }

            public static FileIntegrity CalculateIntegrity(byte[] content)
            {
                var offset = 0;
                var blocks = new List<string>(content.Length / IntegrityBlockSize +
                                              (content.Length % IntegrityBlockSize != 0 ? 1 : 0));

                byte[] hash;
                while (content.Length - offset > IntegrityBlockSize)
                {
                    lock (Lock)
                    {
                        hash = Sha256.ComputeHash(content, offset, IntegrityBlockSize);
                    }

                    blocks.Add(ToHex(hash));
                    offset += IntegrityBlockSize;
                }

                if (content.Length - offset > 0)
                {
                    lock (Lock)
                    {
                        hash = Sha256.ComputeHash(content, offset, content.Length - offset);
                    }

                    blocks.Add(ToHex(hash));
                }

                string hashString;
                if (blocks.Count > 1)
                {
                    lock (Lock)
                    {
                        hash = Sha256.ComputeHash(content);
                    }

                    hashString = ToHex(hash);
                }
                else
                {
                    hashString = blocks[0];
                }

                return new FileIntegrity
                {
                    Algorithm = "SHA256",
                    Blocks = blocks,
                    BlockSize = IntegrityBlockSize,
                    Hash = hashString
                };
            }

            private static string ToHex(byte[] bytes)
            {
                var c = new char[bytes.Length * 2];

                for (int bx = 0, cx = 0; bx < bytes.Length; ++bx, ++cx)
                {
                    var b = (byte)(bytes[bx] >> 4);
                    c[cx] = (char)(b > 9 ? b - 10 + 'a' : b + '0');

                    b = (byte)(bytes[bx] & 0x0F);
                    c[++cx] = (char)(b > 9 ? b - 10 + 'a' : b + '0');
                }

                return new string(c);
            }
        }
    }
}