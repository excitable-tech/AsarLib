using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace AsarLib
{
    public class ElectronExecutable
    {
        public enum FuseState : byte
        {
            Disable = 0x30,
            Enable = 0x31,
            Removed = 0x72,
            Inherit = 0x90
        }

        private static readonly byte[] Sentinel =
        {
            0x64, 0x4C, 0x37, 0x70, 0x4B, 0x47, 0x64, 0x6E, 0x4E, 0x7A, 0x37, 0x39,
            0x36, 0x50, 0x62, 0x62, 0x6A, 0x51, 0x57, 0x4E, 0x4B, 0x6D, 0x48, 0x58,
            0x42, 0x5A, 0x61, 0x42, 0x39, 0x74, 0x73, 0x58
        };

        private static readonly string[] FuseOptions =
        {
            "RunAsNode",
            "EnableCookieEncryption",
            "EnableNodeOptionsEnvironmentVariable",
            "EnableNodeCliInspectArguments",
            "EnableEmbeddedAsarIntegrityValidation",
            "OnlyLoadAppFromAsar",
            "LoadBrowserProcessSpecificV8Snapshot",
            "GrantFileProtocolExtraPrivileges"
        };

        private readonly byte[] _fuse = new byte[32];
        private readonly long _fuseOffset;

        private readonly Stream _stream;

        public ElectronExecutable(Stream stream, int bufferSize = 16 * 1024 * 1024)
        {
            _stream = stream;

            if ((_fuseOffset = FindFuse(bufferSize)) == -1)
                throw new KeyNotFoundException($"{nameof(Sentinel)} not found");

            var position = _fuseOffset + _fuse.Length;
            ReadBlock(_fuse, ref position, 0);
            if (_fuse[0] != 1) throw new NotSupportedException($"Fuse version {_fuse[0]} is not supported");
        }

        public FuseState RunAsNode
        {
            get => GetFuseState(0u);
            set => SetFuseState(0u, value);
        }

        public FuseState EnableCookieEncryption
        {
            get => GetFuseState(1u);
            set => SetFuseState(1u, value);
        }

        public FuseState EnableNodeOptionsEnvironmentVariable
        {
            get => GetFuseState(2u);
            set => SetFuseState(2u, value);
        }

        public FuseState EnableNodeCliInspectArguments
        {
            get => GetFuseState(3u);
            set => SetFuseState(3u, value);
        }

        public FuseState EnableEmbeddedAsarIntegrityValidation
        {
            get => GetFuseState(4u);
            set => SetFuseState(4u, value);
        }

        public FuseState OnlyLoadAppFromAsar
        {
            get => GetFuseState(5u);
            set => SetFuseState(5u, value);
        }

        public FuseState LoadBrowserProcessSpecificV8Snapshot
        {
            get => GetFuseState(6u);
            set => SetFuseState(6u, value);
        }

        public FuseState GrantFileProtocolExtraPrivileges
        {
            get => GetFuseState(7u);
            set => SetFuseState(7u, value);
        }

        private unsafe long FindFuse(int bufferSize)
        {
            if (_stream.Length <= Sentinel.Length) throw new DataException("Stream is too small");
            if (bufferSize <= Sentinel.Length) throw new DataException("Buffer is too small");

            var buffer = new byte[bufferSize];
            var position = _stream.Length - Sentinel.Length;

            fixed (byte* sentinelPtr = Sentinel)
            fixed (byte* bufferPtr = buffer)
            {
                var sentinelByte = *sentinelPtr;
                var sentinelIntPtr = (int*)sentinelPtr;

                while (position > 0)
                {
                    var count = ReadBlock(buffer, ref position, Sentinel.Length);
                    var startPtr = bufferPtr;
                    var endPtr = bufferPtr + count - Sentinel.Length;
                    for (; startPtr < endPtr; startPtr += 1)
                    {
                        if (*startPtr != sentinelByte) continue;
                        var startIntPtr = (int*)startPtr;
                        if (startIntPtr[0] == sentinelIntPtr[0] && startIntPtr[1] == sentinelIntPtr[1] &&
                            startIntPtr[2] == sentinelIntPtr[2] && startIntPtr[3] == sentinelIntPtr[3] &&
                            startIntPtr[4] == sentinelIntPtr[4] && startIntPtr[5] == sentinelIntPtr[5] &&
                            startIntPtr[6] == sentinelIntPtr[6] && startIntPtr[7] == sentinelIntPtr[7])
                            return position + (startPtr - bufferPtr) + Sentinel.Length;
                    }
                }
            }

            return -1;
        }

        private int ReadBlock(byte[] buffer, ref long position, int overlap)
        {
            position += overlap;

            var offset = Math.Max(0, position - buffer.Length);
            var count = (int)(position - offset);
            _stream.Seek(offset, SeekOrigin.Begin);
            var read = _stream.Read(buffer, 0, count);
            if (read != count)
                throw new IOException(
                    $"Failed to read expected number of bytes. Requested {count}, got {read}");

            position = offset;
            return count;
        }

        private void CheckFuse(uint index)
        {
            if (_fuse[1] <= index)
                throw new NotSupportedException(
                    $"Fuse '{(index >= FuseOptions.Length ? index.ToString() : FuseOptions[index])}' doesn't exist in current electron executable");
        }

        public FuseState GetFuseState(uint index)
        {
            CheckFuse(index);
            return (FuseState)_fuse[2 + index];
        }

        private void SetFuseState(uint index, FuseState state)
        {
            CheckFuse(index);
            _fuse[2 + index] = (byte)state;
        }

        public void Save()
        {
            _stream.Seek(_fuseOffset, SeekOrigin.Begin);
            _stream.Write(_fuse, 0, _fuse.Length);
            _stream.Flush();
        }
    }
}