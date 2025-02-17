#nullable disable

using System.Collections.Generic;
using System.IO;
using AsarLib.Json;

namespace AsarLib
{
    public partial class Filesystem
    {
        public class FileEntry
        {
            public FileData Data;

            public bool? Executable;

            public Dictionary<string, FileEntry> Files;

            public string Link;
            public FileType Type;

            public bool? Unpacked;

            private FileEntry Initialize(Filesystem filesystem, long? offset, long? size, FileIntegrity integrity)
            {
                if (Link != null)
                {
                    Type = FileType.Link;
                }
                else if (Files != null)
                {
                    Type = FileType.Directory;
                }
                else
                {
                    Type = FileType.File;
                    Data = new FileData(filesystem, offset, size, integrity);
                }

                return this;
            }

            public JsonWriter Serialize(JsonWriter writer, ref long offset)
            {
                writer.WriteSymbol(JsonSymbol.OpenCurly);
                Data?.SerializeInternal(writer, Executable, Unpacked, ref offset);
                if (Unpacked == true)
                    writer.WritePropertyKey("unpacked").WriteSymbol(JsonSymbol.True).WriteSymbol(JsonSymbol.Comma);
                if (Link != null)
                    writer.WritePropertyKey("link").WriteString(Link).WriteSymbol(JsonSymbol.Comma);
                if (Files != null)
                {
                    writer.WritePropertyKey("files").WriteSymbol(JsonSymbol.OpenCurly);
                    foreach (var file in Files)
                        file.Value.Serialize(writer.WritePropertyKey(file.Key), ref offset)
                            .WriteSymbol(JsonSymbol.Comma);
                    if (Files.Count > 0)
                        writer.RemoveEndBytes(1);
                    writer.WriteSymbol(JsonSymbol.CloseCurly).WriteSymbol(JsonSymbol.Comma);
                }

                return writer.RemoveEndBytes(1).WriteSymbol(JsonSymbol.CloseCurly);
            }

            public static FileEntry Deserialize(Filesystem filesystem, JsonReader reader)
            {
                long? size = null;
                long? offset = null;
                FileIntegrity integrity = null;

                var entry = new FileEntry();
                while (true)
                    switch (reader.TakePropertyName())
                    {
                        case "files":
                            entry.Files = new Dictionary<string, FileEntry>();

                            string name;
                            while ((name = reader.TakePropertyName()) != null)
                                entry.Files.Add(name, Deserialize(filesystem, reader));
                            break;
                        case "size":
                            size = (long)reader.TakeNumber();
                            break;
                        case "integrity":
                            integrity = FileIntegrity.Deserialize(reader);
                            break;
                        case "offset":
                            var str = reader.TakeString();
                            if (!long.TryParse(str, out var value))
                                throw new InvalidDataException($"Malformed offset '{str}'");
                            offset = value;
                            break;
                        case "unpacked":
                            entry.Unpacked = reader.TakeBooleanOrNull();
                            break;
                        case "executable":
                            entry.Executable = reader.TakeBooleanOrNull();
                            break;
                        case "link":
                            entry.Link = reader.TakeString();
                            break;
                        case null:
                            return entry.Initialize(filesystem, offset, size, integrity);
                        default:
                            reader.SkipToken();
                            break;
                    }
            }
        }
    }
}