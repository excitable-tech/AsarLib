using System;
using System.Globalization;
using System.Runtime.InteropServices;
using static AsarLib.Json.ConstantTables;

namespace AsarLib.Json
{
    public unsafe class JsonWriter : IDisposable
    {
        private GCHandle _bufferHandle;
        private byte* _end;
        private byte* _ptr;
        private byte* _start;
        public byte[] Buffer;

        public JsonWriter(int bufferSize = 8 * 1024 * 1024)
        {
            Buffer = new byte[bufferSize];
            _bufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            _start = (byte*)_bufferHandle.AddrOfPinnedObject();
            _ptr = _start;
            _end = _start + Buffer.Length;
        }

        public int Length => (int)(_ptr - _start);

        public void Dispose()
        {
            _bufferHandle.Free();
        }

        public JsonWriter WriteSymbol(JsonSymbol symbol)
        {
            if (_ptr + 4 >= _end) _ptr = Grow();

            switch (symbol)
            {
                case JsonSymbol.Comma:
                case JsonSymbol.Colon:
                case JsonSymbol.OpenSquare:
                case JsonSymbol.CloseSquare:
                case JsonSymbol.OpenCurly:
                case JsonSymbol.CloseCurly:
                    *_ptr = (byte)symbol;
                    break;
                case JsonSymbol.True:
                    *_ptr = 0x74;
                    *(_ptr += 1) = 0x72;
                    *(_ptr += 1) = 0x75;
                    *(_ptr += 1) = 0x65;
                    break;
                case JsonSymbol.False:
                    *_ptr = 0x66;
                    *(_ptr += 1) = 0x61;
                    *(_ptr += 1) = 0x6C;
                    *(_ptr += 1) = 0x73;
                    *(_ptr += 1) = 0x65;
                    break;
                case JsonSymbol.Null:
                    *_ptr = 0x6E;
                    *(_ptr += 1) = 0x75;
                    *(_ptr += 1) = 0x6C;
                    *(_ptr += 1) = 0x6C;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null);
            }

            _ptr += 1;
            return this;
        }

        public JsonWriter WriteULong(ulong value, bool quotes = false)
        {
            if (_ptr + 22 >= _end) _ptr = Grow();

            if (quotes)
            {
                *_ptr = 0x22;
                _ptr += 1;
            }

            var endPtr = _ptr + GetOffset(value);
            _ptr = endPtr + 1;
            while (value >= 10)
            {
                *endPtr = (byte)(0x30 + value % 10);
                endPtr -= 1;
                value /= 10;
            }

            *endPtr = (byte)(0x30 + value);

            if (!quotes) return this;

            *_ptr = 0x22;
            _ptr += 1;
            return this;
        }

        public JsonWriter WriteLong(long value, bool quotes = false)
        {
            if (_ptr + 22 >= _end) _ptr = Grow();

            if (quotes)
            {
                *_ptr = 0x22;
                _ptr += 1;
            }

            if (value < 0)
            {
                *_ptr = 0x2D;
                _ptr += 1;
                if (value == long.MinValue) WriteULong(9223372036854775808UL);
                else WriteULong((ulong)-value);
            }
            else
            {
                WriteULong((ulong)value);
            }

            if (quotes)
            {
                *_ptr = 0x22;
                _ptr += 1;
            }

            return this;
        }

        public JsonWriter WriteDouble(double value)
        {
            return WriteString(value.ToString("G", CultureInfo.InvariantCulture));
        }

        public JsonWriter WriteString(string @string, bool quotes = true)
        {
            if (quotes)
            {
                if (_ptr >= _end) _ptr = Grow();
                *_ptr = 0x22;
                _ptr += 1;
            }

            fixed (char* pString = @string)
            {
                var pChar = pString;
                var end = pChar + @string.Length;
                for (; pChar < end; pChar += 1)
                {
                    if (_ptr + 6 >= _end) _ptr = Grow();

                    var @char = *pChar;
                    switch (@char)
                    {
                        case '\b':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x62;
                            break;
                        case '\t':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x74;
                            break;
                        case '\n':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x6E;
                            break;
                        case '\f':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x66;
                            break;
                        case '\r':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x72;
                            break;
                        case '"':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x22;
                            break;
                        case '\\':
                            *_ptr = 0x5C;
                            *(_ptr += 1) = 0x5C;
                            break;
                        default:
                            if (@char <= 0x1F)
                            {
                                *_ptr = 0x5C;
                                *(_ptr += 1) = 0x75;
                                fixed (byte* table = CharEscapeTable)
                                {
                                    *(int*)(_ptr + 1) = ((int*)table)[@char];
                                }

                                _ptr += 4;
                            }
                            else if (@char <= 0x7F)
                            {
                                *_ptr = (byte)@char;
                            }
                            else if (@char <= 0x07FF)
                            {
                                *_ptr = (byte)(((@char >> 6) & 0x1F) | 0xC0);
                                *(_ptr += 1) = (byte)(((@char >> 0) & 0x3F) | 0x80);
                            }
                            else if (@char <= 0xFFFF)
                            {
                                if (@char >= 0xD800 && @char <= 0xDFFF)
                                {
                                    *_ptr = 0x5C;
                                    *(_ptr += 1) = 0x75;
                                    fixed (byte* table = SurrogateEscapeTable)
                                    {
                                        *(int*)(_ptr + 1) = ((int*)table)[@char - 0xD800];
                                    }

                                    _ptr += 4;
                                }
                                else
                                {
                                    *_ptr = (byte)(((@char >> 12) & 0x0F) | 0xE0);
                                    *(_ptr += 1) = (byte)(((@char >> 6) & 0x3F) | 0x80);
                                    *(_ptr += 1) = (byte)(((@char >> 0) & 0x3F) | 0x80);
                                }
                            }
                            else
                            {
                                throw JsonException.InvalidChar(@char);
                            }

                            break;
                    }

                    _ptr += 1;
                }
            }

            if (!quotes) return this;
            if (_ptr >= _end) _ptr = Grow();
            *_ptr = 0x22;
            _ptr += 1;
            return this;
        }

        public JsonWriter WritePropertyKey(string name)
        {
            WriteString(name);
            if (_ptr >= _end) _ptr = Grow();
            *_ptr = 0x3A;
            _ptr += 1;
            return this;
        }

        public JsonWriter RemoveEndBytes(int count)
        {
            _ptr -= count;
            return this;
        }

        private byte* Grow()
        {
            _bufferHandle.Free();

            var buffer = new byte[Buffer.Length * 2];
            var length = (int)(_ptr - _start);
            System.Buffer.BlockCopy(Buffer, 0, buffer, 0, length);

            Buffer = buffer;
            _bufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            _start = (byte*)_bufferHandle.AddrOfPinnedObject();
            _end = _start + buffer.Length;
            return _ptr = _start + length;
        }

        private static int GetOffset(ulong value)
        {
            if (value <= 999999999999UL)
                if (value <= 99999999UL)
                    if (value <= 9999UL)
                        if (value <= 99UL) return value > 9UL ? 1 : 0;
                        else return value > 999UL ? 3 : 2;
                    else if (value <= 999999UL) return value > 99999UL ? 5 : 4;
                    else return value > 9999999UL ? 7 : 6;
                else if (value <= 9999999999UL) return value > 999999999UL ? 9 : 8;
                else return value > 99999999999UL ? 11 : 10;
            if (value <= 9999999999999999UL)
                if (value <= 99999999999999UL) return value > 9999999999999UL ? 13 : 12;
                else return value > 999999999999999UL ? 15 : 14;
            if (value <= 999999999999999999UL) return value > 99999999999999999UL ? 17 : 16;
            return value > 9999999999999999999UL ? 19 : 18;
        }
    }
}