using System;

namespace AsarLib.Json
{
    public class JsonException : Exception
    {
        public JsonException(string message) : base(message)
        {
        }

        public static JsonException UnexpectedEndOfJson => new JsonException("Unexpected end of json");

        public static JsonException UnexpectedToken(long offset)
        {
            return new JsonException($"Unexpected token at {offset}");
        }

        public static JsonException InvalidNumber(long offset)
        {
            return new JsonException($"Invalid number at {offset}");
        }

        public static JsonException InvalidCodepoint(long offset)
        {
            return new JsonException($"Invalid codepoint at {offset}");
        }

        public static JsonException InvalidSurrogate(long offset)
        {
            return new JsonException($"Invalid surrogate at {offset}");
        }

        public static JsonException InvalidChar(char @char)
        {
            return new JsonException($"Invalid char '{@char}'");
        }
    }
}