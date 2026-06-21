using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MaxCHUIM.DataStructures;

public sealed class BitVector64
{
    private ulong[] _words;
    private int _bitLength;

    public BitVector64(int initialCapacity = 64)
    {
        int initialWordCapacity = (initialCapacity + 63) / 64;
        _words = new ulong[initialWordCapacity > 0 ? initialWordCapacity : 1];
        _bitLength = 0;
    }

    public BitVector64(ulong[] words, int bitLength)
    {
        _words = words;
        _bitLength = bitLength;
    }

    public int BitLength => _bitLength;
    public int WordCount => (_bitLength + 63) / 64;
    public ReadOnlySpan<ulong> Words => new ReadOnlySpan<ulong>(_words, 0, WordCount);

    public bool Get(int i)
    {
        if (i < 0 || i >= _bitLength) return false;
        int wordIndex = i / 64;
        int bitIndex = i % 64;
        return (_words[wordIndex] & (1UL << bitIndex)) != 0;
    }

    public void Set(int i, bool value)
    {
        if (i < 0 || i >= _bitLength) return;
        int wordIndex = i / 64;
        int bitIndex = i % 64;
        if (value)
            _words[wordIndex] |= (1UL << bitIndex);
        else
            _words[wordIndex] &= ~(1UL << bitIndex);
    }

    public void AppendBit(bool value)
    {
        int wordIndex = _bitLength / 64;
        if (wordIndex >= _words.Length)
        {
            Array.Resize(ref _words, _words.Length * 2);
        }

        if (value)
        {
            int bitIndex = _bitLength % 64;
            _words[wordIndex] |= (1UL << bitIndex);
        }
        else
        {
            // Bit is already 0, just need to make sure
            int bitIndex = _bitLength % 64;
            _words[wordIndex] &= ~(1UL << bitIndex);
        }

        _bitLength++;
    }

    public bool IsZero()
    {
        int wc = WordCount;
        for (int i = 0; i < wc; i++)
        {
            if (_words[i] != 0) return false;
        }
        return true;
    }

    public int PopCount()
    {
        int count = 0;
        int wc = WordCount;
        for (int i = 0; i < wc; i++)
        {
            count += BitOperations.PopCount(_words[i]);
        }
        return count;
    }

    public void AndInPlace(BitVector64 other)
    {
        int wc = Math.Min(WordCount, other.WordCount);
        for (int i = 0; i < wc; i++)
        {
            _words[i] &= other._words[i];
        }
        // clear the rest
        for (int i = wc; i < WordCount; i++)
        {
            _words[i] = 0;
        }
    }

    public void OrInPlace(BitVector64 other)
    {
        EnsureWordCapacity(other.WordCount);
        int wc = other.WordCount;
        for (int i = 0; i < wc; i++)
        {
            _words[i] |= other._words[i];
        }
        _bitLength = Math.Max(_bitLength, other._bitLength);
    }

    public void NotInPlace()
    {
        int wc = WordCount;
        for (int i = 0; i < wc; i++)
        {
            _words[i] = ~_words[i];
        }

        // Mask out unused bits in the last word
        int bitsInLastWord = _bitLength % 64;
        if (bitsInLastWord > 0)
        {
            _words[wc - 1] &= (1UL << bitsInLastWord) - 1;
        }
    }

    public void ResetAllOnes(int length)
    {
        _bitLength = length;
        int wc = WordCount;
        EnsureWordCapacity(wc);
        for(int i=0; i<wc; i++)
        {
            _words[i] = ulong.MaxValue;
        }
        int bitsInLastWord = _bitLength % 64;
        if (bitsInLastWord > 0)
        {
            _words[wc - 1] &= (1UL << bitsInLastWord) - 1;
        }
    }

    private void EnsureWordCapacity(int wordCapacity)
    {
        if (_words.Length < wordCapacity)
        {
            int newSize = _words.Length;
            while (newSize < wordCapacity) newSize *= 2;
            Array.Resize(ref _words, newSize);
        }
    }

    public static BitVector64 And(BitVector64 a, BitVector64 b)
    {
        int bitLength = Math.Min(a.BitLength, b.BitLength);
        int wc = (bitLength + 63) / 64;
        ulong[] words = new ulong[wc];
        for (int i = 0; i < wc; i++)
        {
            words[i] = a._words[i] & b._words[i];
        }
        return new BitVector64(words, bitLength);
    }

    public static BitVector64 Or(BitVector64 a, BitVector64 b)
    {
        int bitLength = Math.Max(a.BitLength, b.BitLength);
        int wc = (bitLength + 63) / 64;
        ulong[] words = new ulong[wc];
        int wcMin = Math.Min(a.WordCount, b.WordCount);
        for (int i = 0; i < wcMin; i++)
        {
            words[i] = a._words[i] | b._words[i];
        }
        if (a.WordCount > wcMin)
        {
            Array.Copy(a._words, wcMin, words, wcMin, a.WordCount - wcMin);
        }
        else if (b.WordCount > wcMin)
        {
            Array.Copy(b._words, wcMin, words, wcMin, b.WordCount - wcMin);
        }
        return new BitVector64(words, bitLength);
    }

    public static BitVector64 Not(BitVector64 a)
    {
        ulong[] words = new ulong[a.WordCount];
        for (int i = 0; i < a.WordCount; i++)
        {
            words[i] = ~a._words[i];
        }
        int bitsInLastWord = a.BitLength % 64;
        if (bitsInLastWord > 0)
        {
            words[a.WordCount - 1] &= (1UL << bitsInLastWord) - 1;
        }
        return new BitVector64(words, a.BitLength);
    }

    public static bool AndIsNonZero(BitVector64 a, BitVector64 b)
    {
        int len = Math.Min(a.WordCount, b.WordCount);
        int i = 0;
        if (Avx2.IsSupported && len >= 4)
        {
            for (; i + 4 <= len; i += 4)
            {
                var va = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(a.Words[i..]));
                var vb = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(b.Words[i..]));
                if (!Vector256.EqualsAll(Vector256.BitwiseAnd(va, vb), Vector256<ulong>.Zero))
                    return true;
            }
        }
        for (; i < len; i++) if ((a._words[i] & b._words[i]) != 0) return true;
        return false;
    }
}
