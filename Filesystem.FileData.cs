using System;
using System.IO;
using System.Text;
using AsarLib.Json;

namespace AsarLib
{
    public partial class Filesystem
    {
        public class FileData
        {
            private readonly Filesystem? _filesystem;
            private readonly long? _offset;
            private readonly long _size;

            private byte[]? _data;
            public bool? Executable;
            public FileIntegrity? Integrity;

            public FileData(string data, bool? executable = null, bool useIntegrity = true) : this(
                Encoding.UTF8.GetBytes(data ?? throw new ArgumentNullException(nameof(data))), executable, useIntegrity)
            {
            }

            public FileData(byte[] data, bool? executable = null, bool useIntegrity = true)
            {
                _data = data ?? throw new ArgumentNullException(nameof(data));
                Executable = executable;
                if (useIntegrity)
                    Integrity = FileIntegrity.CalculateIntegrity(data);
            }

            internal FileData(Filesystem? filesystem, long? offset, long? size, FileIntegrity? integrity)
            {
                _filesystem = filesystem ?? throw new ArgumentNullException(nameof(filesystem));
                _size = size ?? throw new ArgumentNullException(nameof(size));
                if (offset != null)
                    _offset = filesystem._dataOffset + offset;
                Integrity = integrity;
            }

            public byte[]? GetBytes()
            {
                if (_data != null) return _data;
                if (_offset == null) throw new ArgumentNullException(nameof(_offset));

                if (_offset.Value >= _filesystem!._stream!.Length)
                    return null;

                lock (_filesystem._lock)
                {
                    var position = _filesystem._stream.Position;
                    _filesystem._stream.Seek(_offset.Value, SeekOrigin.Begin);
                    var bytes = _filesystem._reader!.ReadBytes((int)_size);
                    _filesystem._stream.Seek(position, SeekOrigin.Begin);
                    return bytes;
                }
            }

            public string? GetString()
            {
                var bytes = GetBytes();
                return bytes == null ? null : Encoding.UTF8.GetString(bytes);
            }

            public long GetSize()
            {
                return _data?.Length ?? _size;
            }

            public void Override(byte[] data)
            {
                _data = data;
                if (Integrity != null)
                    Integrity = FileIntegrity.CalculateIntegrity(data);
            }

            public void Override(string data)
            {
                Override(Encoding.UTF8.GetBytes(data));
            }

            internal JsonWriter SerializeInternal(JsonWriter writer, bool? executable, bool? unpacked, ref long offset)
            {
                writer.WritePropertyKey("size").WriteLong(GetSize()).WriteSymbol(JsonSymbol.Comma);
                Integrity?.Serialize(writer.WritePropertyKey("integrity")).WriteSymbol(JsonSymbol.Comma);

                if (unpacked == true) return writer;

                if (executable == true)
                    writer.WritePropertyKey("executable").WriteSymbol(JsonSymbol.True).WriteSymbol(JsonSymbol.Comma);
                if (_data != null)
                {
                    writer.WritePropertyKey("offset").WriteLong(offset, true).WriteSymbol(JsonSymbol.Comma);
                    offset += _data.Length;
                }
                else if (_offset != null && _filesystem != null)
                {
                    if (_offset >= _filesystem._stream!.Length)
                    {
                        writer.WritePropertyKey("offset").WriteLong(_offset.Value - _filesystem._dataOffset, true)
                            .WriteSymbol(JsonSymbol.Comma);
                    }
                    else
                    {
                        writer.WritePropertyKey("offset").WriteLong(offset, true).WriteSymbol(JsonSymbol.Comma);
                        offset += _size;
                    }
                }
                else
                {
                    throw new InvalidDataException("Invalid FileData");
                }

                return writer;
            }
        }
    }
}
