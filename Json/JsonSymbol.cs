namespace AsarLib.Json
{
    public enum JsonSymbol : byte
    {
        Comma = 0x2C,
        Colon = 0x3A,
        OpenSquare = 0x5B,
        CloseSquare = 0x5D,
        OpenCurly = 0x7B,
        CloseCurly = 0x7D,
        True,
        False,
        Null
    }
}