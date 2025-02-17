using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using static AsarLib.Json.ConstantTables;

#nullable disable

namespace AsarLib.Json
{
    public unsafe class JsonReader : IDisposable
    {
        private readonly byte* _end;
        private readonly byte* _start;

        private byte[] _buffer;
        private byte* _bufferEnd;
        private GCHandle _bufferHandle;
        private byte* _bufferStart;

        private GCHandle _handle;
        private byte* _ptr;

        public JsonReader(byte[] json, int bufferSize = 4 * 1024 * 1024)
        {
            _buffer = new byte[bufferSize];
            _handle = GCHandle.Alloc(json, GCHandleType.Pinned);
            _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _start = (byte*)_handle.AddrOfPinnedObject();
            _bufferStart = (byte*)_bufferHandle.AddrOfPinnedObject();
            _end = _start + json.Length;
            _bufferEnd = _bufferStart + _buffer.Length;
            _ptr = _start;
        }

        public void Dispose()
        {
            _bufferHandle.Free();
            _handle.Free();
        }

        private JsonToken PeekToken()
        {
            while (_ptr < _end)
            {
                switch (*_ptr)
                {
                    case 0x09:
                    case 0x0A:
                    case 0x0D:
                    case 0x20:
                        _ptr++;
                        continue;
                    case 0x22:
                        return JsonToken.String;
                    case 0x2D:
                    case 0x30:
                    case 0x31:
                    case 0x32:
                    case 0x33:
                    case 0x34:
                    case 0x35:
                    case 0x36:
                    case 0x37:
                    case 0x39:
                    case 0x38:
                        return JsonToken.Number;
                    case 0x5B:
                        return JsonToken.Array;
                    case 0x66:
                        if (_ptr[1] == 0x61 && _ptr[2] == 0x6C && _ptr[3] == 0x73 && _ptr[4] == 0x65)
                            return JsonToken.False;
                        break;
                    case 0x6E:
                        if (_ptr[1] == 0x75 && _ptr[2] == 0x6C && _ptr[3] == 0x6C)
                            return JsonToken.Null;
                        break;
                    case 0x74:
                        if (_ptr[1] == 0x72 && _ptr[2] == 0x75 && _ptr[3] == 0x65)
                            return JsonToken.True;
                        break;
                    case 0x7B:
                        return JsonToken.Object;
                }

                throw JsonException.UnexpectedToken(_ptr - _start);
            }

            throw JsonException.UnexpectedEndOfJson;
        }

        public void SkipToken()
        {
            switch (PeekToken())
            {
                case JsonToken.String:
                    _ = TakeString();
                    break;
                case JsonToken.Array:
                    while (TakeNextArrayElement())
                        SkipToken();
                    break;
                case JsonToken.Object:
                    while (TakePropertyName() != null)
                        SkipToken();
                    break;
                case JsonToken.Number:
                    _ = TakeNumber();
                    break;
                case JsonToken.False:
                    _ptr += 5;
                    break;
                case JsonToken.True:
                case JsonToken.Null:
                    _ptr += 4;
                    break;
                default:
                    throw JsonException.UnexpectedToken(_ptr - _start);
            }
        }

        public string TakePropertyName()
        {
            while (_ptr < _end)
            {
                switch (*_ptr)
                {
                    case 0x09:
                    case 0x0A:
                    case 0x0D:
                    case 0x20:
                    case 0x2C:
                    case 0x7B:
                        _ptr++;
                        continue;
                    case 0x22:
                        var key = TakeString();
                        while (_ptr < _end)
                            switch (*_ptr)
                            {
                                case 0x09:
                                case 0x0A:
                                case 0x0D:
                                case 0x20:
                                    _ptr++;
                                    continue;
                                case 0x3A:
                                    _ptr++;
                                    return key;
                                default:
                                    throw JsonException.UnexpectedToken(_ptr - _start);
                            }

                        throw JsonException.UnexpectedEndOfJson;
                    case 0x7D:
                        _ptr++;
                        return null;
                }

                throw JsonException.UnexpectedToken(_ptr - _start);
            }

            throw JsonException.UnexpectedEndOfJson;
        }

        public bool TakeNextArrayElement()
        {
            while (_ptr < _end)
            {
                switch (*_ptr)
                {
                    case 0x09:
                    case 0x0A:
                    case 0x0D:
                    case 0x20:
                        _ptr++;
                        continue;
                    case 0x2C:
                        _ptr++;
                        return true;
                    case 0x5B:
                        var ptr = ++_ptr;
                        while (ptr < _end)
                            switch (*ptr)
                            {
                                case 0x09:
                                case 0x0A:
                                case 0x0D:
                                case 0x20:
                                    ptr++;
                                    continue;
                                case 0x5D:
                                    _ptr = ptr + 1;
                                    return false;
                                default:
                                    return true;
                            }

                        break;
                    case 0x5D:
                        _ptr++;
                        return false;
                }

                throw JsonException.UnexpectedToken(_ptr - _start);
            }

            throw JsonException.UnexpectedEndOfJson;
        }

        public bool? TakeBooleanOrNull()
        {
            while (_ptr < _end)
            {
                switch (*_ptr)
                {
                    case 0x09:
                    case 0x0A:
                    case 0x0D:
                    case 0x20:
                        _ptr++;
                        continue;
                    case 0x66:
                        if (_ptr + 4 >= _end) throw JsonException.UnexpectedEndOfJson;
                        if (*(_ptr += 1) == 0x61 && *(_ptr += 1) == 0x6C && *(_ptr += 1) == 0x73 &&
                            *(_ptr += 1) == 0x65)
                        {
                            _ptr += 1;
                            return false;
                        }

                        break;
                    case 0x6E:
                        if (_ptr + 3 >= _end) throw JsonException.UnexpectedEndOfJson;
                        if (*(_ptr += 1) == 0x75 && *(_ptr += 1) == 0x6C && *(_ptr += 1) == 0x6C)
                        {
                            _ptr += 1;
                            return null;
                        }

                        break;
                    case 0x74:
                        if (_ptr + 3 >= _end) throw JsonException.UnexpectedEndOfJson;
                        if (*(_ptr += 1) == 0x72 && *(_ptr += 1) == 0x75 && *(_ptr += 1) == 0x65)
                        {
                            _ptr += 1;
                            return true;
                        }

                        break;
                }

                throw JsonException.UnexpectedToken(_ptr - _start);
            }

            throw JsonException.UnexpectedEndOfJson;
        }

        public double TakeNumber()
        {
            if (_ptr >= _end) throw JsonException.UnexpectedEndOfJson;

            var start = _ptr;
            var @byte = *_ptr;

            if (@byte == 0x2B) throw JsonException.InvalidNumber(start - _start);
            if (@byte == 0x2D)
            {
                if ((_ptr += 1) >= _end) throw JsonException.UnexpectedEndOfJson;
                @byte = *_ptr;
            }

            if (@byte == 0x30)
            {
                if ((_ptr += 1) >= _end) return 0.0;
                @byte = *_ptr;

                if (@byte == 0x2E || @byte == 0x45 || @byte == 0x65)
                {
                    if ((_ptr += 1) >= _end) throw JsonException.UnexpectedEndOfJson;
                    @byte = *_ptr;
                }
                else if (@byte != 0x2C && @byte != 0x5D && @byte != 0x7D)
                {
                    throw JsonException.InvalidNumber(start - _start);
                }
            }
            else if (@byte == 0x2E)
            {
                throw JsonException.InvalidNumber(start - _start);
            }

            for (;; @byte = *_ptr)
            {
                if (@byte <= 0x20)
                {
                    if ((byte)(@byte - 0x09) <= 0x1 || @byte == 0x0D || @byte == 0x20)
                        break;
                }
                else if (@byte == 0x2C || @byte == 0x5D || @byte == 0x7D)
                {
                    break;
                }

                if ((_ptr += 1) >= _end) break;
            }

            if (!double.TryParse(new string((sbyte*)start, 0, (int)(_ptr - start)), NumberStyles.Float,
                    NumberFormatInfo.InvariantInfo, out var number))
                throw JsonException.InvalidNumber(start - _start);
            return number;
        }

        public string TakeString()
        {
            _ptr++;
            var buffer = _bufferStart;
            for (; _ptr < _end; _ptr += 1, buffer += 1)
            {
                var @byte = *_ptr;
                if (@byte == 0x22)
                {
                    _ptr += 1;
                    return Encoding.UTF8.GetString(_buffer, 0, (int)(buffer - _bufferStart));
                }

                if (buffer + 3 >= _bufferEnd) buffer = Grow();

                if (@byte == 0x5C)
                {
                    if ((_ptr += 1) >= _end) throw JsonException.UnexpectedEndOfJson;
                    switch (@byte = *_ptr)
                    {
                        case 0x22:
                        case 0x2F:
                        case 0x5C:
                            *buffer = @byte;
                            break;
                        case 0x62:
                            *buffer = 0x08;
                            break;
                        case 0x66:
                            *buffer = 0x0C;
                            break;
                        case 0x6E:
                            *buffer = 0x0A;
                            break;
                        case 0x72:
                            *buffer = 0x0D;
                            break;
                        case 0x74:
                            *buffer = 0x09;
                            break;
                        case 0x75:
                            if ((_ptr += 1) + 3 >= _end) throw JsonException.UnexpectedEndOfJson;

                            byte byte0, byte1, byte2, byte3;
                            if ((byte0 = HexTable[*_ptr]) == 0xFF || (byte1 = HexTable[*(_ptr += 1)]) == 0xFF ||
                                (byte2 = HexTable[*(_ptr += 1)]) == 0xFF || (byte3 = HexTable[*(_ptr += 1)]) == 0xFF)
                                throw JsonException.InvalidCodepoint(_ptr - _start);

                            var codePoint = (uint)((byte0 << 0x0C) | (byte1 << 0x08) | (byte2 << 0x04) | byte3);
                            if (0xDC00 <= codePoint && codePoint <= 0xDFFF)
                                throw JsonException.InvalidSurrogate(_ptr - _start);

                            if (0xD800 <= codePoint && codePoint <= 0xDBFF)
                            {
                                if ((_ptr += 1) + 5 >= _end) throw JsonException.UnexpectedEndOfJson;
                                if ((@byte = *_ptr) != 0x5C || (@byte = *(_ptr += 1)) != 0x75)
                                    throw JsonException.UnexpectedToken(_ptr - _start);

                                if ((byte0 = HexTable[*(_ptr += 1)]) == 0xFF ||
                                    (byte1 = HexTable[*(_ptr += 1)]) == 0xFF ||
                                    (byte2 = HexTable[*(_ptr += 1)]) == 0xFF ||
                                    (byte3 = HexTable[*(_ptr += 1)]) == 0xFF)
                                    throw JsonException.InvalidCodepoint(_ptr - _start);

                                var surrogate = (uint)((byte0 << 0x0C) | (byte1 << 0x08) | (byte2 << 0x04) | byte3);
                                if (0xDC00 > surrogate || surrogate > 0xDFFF)
                                    throw JsonException.InvalidSurrogate(_ptr - _start);

                                codePoint = ((codePoint - 0xD800) << 10) + (surrogate - 0xDC00) + 0x10000;
                            }

                            if (codePoint <= 0x7F)
                            {
                                *buffer = (byte)codePoint;
                            }
                            else if (codePoint <= 0x07FF)
                            {
                                *buffer = (byte)(((codePoint >> 6) & 0x1F) | 0xC0);
                                *(buffer += 1) = (byte)(((codePoint >> 0) & 0x3F) | 0x80);
                            }
                            else if (codePoint <= 0xFFFF)
                            {
                                *buffer = (byte)(((codePoint >> 12) & 0x0F) | 0xE0);
                                *(buffer += 1) = (byte)(((codePoint >> 6) & 0x3F) | 0x80);
                                *(buffer += 1) = (byte)(((codePoint >> 0) & 0x3F) | 0x80);
                            }
                            else if (codePoint <= 0x10FFFF)
                            {
                                *buffer = (byte)(((codePoint >> 18) & 0x07) | 0xF0);
                                *(buffer += 1) = (byte)(((codePoint >> 12) & 0x3F) | 0x80);
                                *(buffer += 1) = (byte)(((codePoint >> 6) & 0x3F) | 0x80);
                                *(buffer += 1) = (byte)(((codePoint >> 0) & 0x3F) | 0x80);
                            }
                            else
                            {
                                throw JsonException.InvalidCodepoint(_ptr - _start);
                            }

                            break;
                        default:
                            throw JsonException.UnexpectedToken(_ptr - _start);
                    }
                }
                else
                {
                    *buffer = @byte;
                }
            }

            throw JsonException.UnexpectedEndOfJson;
        }

        public void Reset()
        {
            _ptr = _start;
        }

        public byte* Grow()
        {
            _bufferHandle.Free();

            var buffer = new byte[_buffer.Length * 2];
            var length = (int)(_ptr - _start);
            Buffer.BlockCopy(_buffer, 0, buffer, 0, length);

            _buffer = buffer;
            _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferStart = (byte*)_bufferHandle.AddrOfPinnedObject();
            _bufferEnd = _bufferStart + buffer.Length;
            return _bufferStart + length;
        }
    }
}