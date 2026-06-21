using System.Collections.Generic;

namespace MaxCHUIM.DataStructures;

public sealed class BmChui
{
    private readonly Dictionary<int, BitVector64> _bm = new();
    private int _chuiCount = 0;

    public int ChuiCount => _chuiCount;

    public BitVector64 GetOrCreate(int item)
    {
        if (!_bm.TryGetValue(item, out var bv))
        {
            bv = new BitVector64();
            _bm[item] = bv;
        }
        
        // Pad with false up to current count
        while (bv.BitLength < _chuiCount)
        {
            bv.AppendBit(false);
        }
        
        return bv;
    }

    public int Append(IReadOnlyList<int> C)
    {
        foreach (var item in C)
        {
            var bv = GetOrCreate(item);
            bv.AppendBit(true);
        }

        int index = _chuiCount;
        _chuiCount++;
        
        // We defer padding for items NOT in C to GetOrCreate. 
        // This is safe because GetOrCreate will append 'false' for the missing bits 
        // up to _chuiCount when accessed.
        
        return index;
    }

    public BitVector64 Intersect(IReadOnlyList<int> X)
    {
        if (X.Count == 0) return new BitVector64();

        BitVector64 result = null;
        for (int i = 0; i < X.Count; i++)
        {
            var bv = GetOrCreate(X[i]);
            // Ensure padding is up to date before intersecting
            while (bv.BitLength < _chuiCount) bv.AppendBit(false);
            
            if (result == null)
            {
                result = BitVector64.Or(bv, new BitVector64()); // Copy
            }
            else
            {
                result.AndInPlace(bv);
            }
        }
        
        return result ?? new BitVector64();
    }
}
