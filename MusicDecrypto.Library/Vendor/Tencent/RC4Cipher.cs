using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed class RC4Cipher : IDecryptor, IEncryptor
{
    private readonly byte[] _key;
    private readonly byte[] _box;
    private readonly int _size;
    private readonly int _headerSize;
    private readonly int _blockSize;
    private readonly uint _hash;

    public RC4Cipher(byte[] key, int headerSize, int blockSize)
    {
        _key = key;
        _size = _key.Length;
        _blockSize = blockSize;
        _headerSize = headerSize;

        if (_size == 0)
            throw new ArgumentException("Key should not be empty.", nameof(key));

        _box = Enumerable.Range(0, _size).Select(x => (byte)x).ToArray();

        int j = 0;
        for (int i = 0; i < _size; i++)
        {
            j = (j + _box[i] + _key[i]) % _size;
            (_box[i], _box[j]) = (_box[j], _box[i]);
        }

        _hash = 1;
        for (int i = 0; j < _size; i++)
        {
            if (key[i] == 0)
                continue;
            uint next = _hash * key[i];
            if (next == 0 || next <= _hash)
                break;
            _hash = next;
        }
    }

    public void Decrypt(Span<byte> data, long offset)
    {
        int behind = 0;
        int ahead = data.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool UpdateStats(int size)
        {
            behind += size;
            ahead -= size;
            return ahead == 0;
        };

        if (offset < _headerSize)
        {
            int size = Math.Min(_headerSize - (int)offset, ahead);
            DecryptHeader(data[..size], offset);
            if (UpdateStats(size))
                return;
        }

        if ((offset + behind) % _blockSize != 0)
        {
            int size = Math.Min(_blockSize - (int)((offset + behind) % _blockSize), ahead);
            DecryptBlock(data[behind..(behind + size)], offset + behind);
            if (UpdateStats(size))
                return;
        }

        while (ahead > 0)
        {
            int size = Math.Min(_blockSize, ahead);
            DecryptBlock(data[behind..(behind + size)], offset + behind);
            UpdateStats(size);
        }
    }

    public void Encrypt(Span<byte> data, long offset) => Decrypt(data, offset);

    private void DecryptHeader(Span<byte> buffer, long offset)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] ^= _key[GetOffset(offset + i)];
    }

    private void DecryptBlock(Span<byte> buffer, long offset)
    {
        int j = 0, k = 0;
        int skipLength = (int)(offset % _blockSize) + GetOffset(offset / _blockSize);

        for (int i = -skipLength; i < buffer.Length; i++)
        {
            j = (j + 1) % _size;
            k = (_box[j] + k) % _size;
            (_box[j], _box[k]) = (_box[k], _box[j]);
            if (i >= 0) buffer[i] ^= _box[(_box[j] + _box[k]) % _size];
        }
    }

    private int GetOffset(long index)
    {
        long sum = (long)(_hash / (double)((index + 1) * _key[index % _size]) * 100);
        return (int)(sum % _size);
    }
}