using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AsarLib.Json;

namespace AsarLib
{
    public partial class Filesystem : IDisposable
    {
        private readonly object _lock = new object();
        private readonly BinaryReader? _reader;
        private readonly FileEntry _root;

        private readonly Stream? _stream;
        public readonly string? IntegrityHash;

        private uint _dataOffset;

        public Filesystem()
        {
            _root = new FileEntry
            {
                Type = FileType.Directory,
                Files = new Dictionary<string, FileEntry>()
            };
        }

        public Filesystem(Stream stream)
        {
            _stream = stream;
            _reader = new BinaryReader(stream, Encoding.UTF8, true);
            _root = ReadHeader(out IntegrityHash);
        }

        public Dictionary<string, FileEntry> Files => _root.Files!;

        public void Dispose()
        {
            _reader?.Dispose();
            _stream?.Dispose();
        }

        private FileEntry ReadHeader(out string integrityHash)
        {
            if (_reader!.ReadUInt32() != 4u)
                throw new InvalidDataException("Invalid header format");

            var headerSize = _reader.ReadUInt32();
            if (headerSize > _reader.BaseStream.Length) throw new InvalidDataException("Invalid header format");

            var pickleSize = (int)(headerSize - _reader.ReadUInt32());
            if (pickleSize != 4) throw new InvalidDataException("Invalid header format");

            var stringSize = _reader.ReadInt32();
            if (stringSize < 0 || AlignInt(stringSize, 4) != headerSize - 8)
                throw new InvalidDataException("Invalid header format");

            _dataOffset = headerSize + 8;

            var header = _reader.ReadBytes(stringSize);
            integrityHash = FileIntegrity.ComputeHash(header, 0, header.Length);

            using (var reader = new JsonReader(header))
            {
                return FileEntry.Deserialize(this, reader);
            }
        }

        private void WriteRawFilesystem(Stream stream, FileEntry entry)
        {
            switch (entry.Type)
            {
                case FileType.Directory:
                    if (entry.Files != null)
                        foreach (var file in entry.Files)
                            WriteRawFilesystem(stream, file.Value);
                    break;
                case FileType.File:
                    if (entry.Unpacked != true)
                    {
                        var bytes = entry.Data?.GetBytes();
                        if (bytes != null)
                            stream.Write(bytes, 0, bytes.Length);
                    }

                    break;
                case FileType.Link:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Type));
            }
        }

        public void Save(Stream stream, out string integrityHash)
        {
            using (var json = new JsonWriter())
            {
                var offset = 0L;
                _root.Serialize(json, ref offset);
                integrityHash = FileIntegrity.ComputeHash(json.Buffer, 0, json.Length);
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    var length = json.Length;
                    var headerLength = AlignInt(length, 4);
                    writer.Write(4);
                    writer.Write(headerLength + 8);
                    writer.Write(headerLength + 4);
                    writer.Write(length);
                    stream.Write(json.Buffer, 0, length);
                    var padding = new byte[headerLength - length];
                    stream.Write(padding, 0, padding.Length);
                    writer.Flush();
                }
            }

            foreach (var file in Files)
                WriteRawFilesystem(stream, file.Value);
            stream.Flush();
        }

        private static int AlignInt(int number, int alignment)
        {
            return number + (alignment - number % alignment) % alignment;
        }
    }
}