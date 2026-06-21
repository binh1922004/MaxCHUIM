using System;

namespace MaxCHUIM.DataStructures;

public sealed class ValidityMask
{
    private BitVector64 _v;

    public ValidityMask()
    {
        _v = new BitVector64();
    }

    public BitVector64 Vector => _v;

    public void AppendValid()
    {
        _v.AppendBit(true);
    }

    public void InvalidateMany(BitVector64 maskSubset)
    {
        // V := V ∧ ¬maskSubset
        // We assume we own maskSubset and can mutate it.
        maskSubset.NotInPlace();
        _v.AndInPlace(maskSubset);
    }

    public double Density()
    {
        if (_v.BitLength == 0) return 1.0;
        return (double)_v.PopCount() / _v.BitLength;
    }

    public void ResetAllOnes(int validCount)
    {
        _v.ResetAllOnes(validCount);
    }
}
