using System.Collections.Generic;

namespace MaxCHUIM.DataStructures;

public sealed class MaxBitmax
{
    private readonly Dictionary<int, BitVector64> _bm = new();
    private int _maxHuiCount = 0;

    public int MaxHuiCount => _maxHuiCount;

    public BitVector64 GetOrCreate(int item)
    {
        if (!_bm.TryGetValue(item, out var bv))
        {
            bv = new BitVector64();
            // In case we've already added some MaxHUIs before this item is first queried/added,
            // we should pad it with 0s up to the current count.
            while (bv.BitLength < _maxHuiCount)
            {
                bv.AppendBit(false);
            }
            _bm[item] = bv;
        }
        return bv;
    }

    public int AppendMaxHui(IReadOnlyList<int> M, IEnumerable<int> universe)
    {
        var mSet = new HashSet<int>(M);
        foreach (var item in universe)
        {
            var bv = GetOrCreate(item);
            bv.AppendBit(mSet.Contains(item));
        }
        int index = _maxHuiCount;
        _maxHuiCount++;
        return index;
    }

    public BitVector64 Intersect(IReadOnlyList<int> X)
    {
        if (X.Count == 0) return new BitVector64();

        BitVector64 result = null;
        for (int i = 0; i < X.Count; i++)
        {
            var bv = GetOrCreate(X[i]);
            if (result == null)
            {
                result = BitVector64.Or(bv, new BitVector64()); // Copy to avoid mutating original
            }
            else
            {
                result.AndInPlace(bv);
            }
        }
        return result ?? new BitVector64();
    }

    public void RebuildFromValidColumns(int item, IReadOnlyList<int> validIdx)
    {
        if (!_bm.TryGetValue(item, out var oldBv)) return;
        
        var newBv = new BitVector64();
        foreach (var idx in validIdx)
        {
            newBv.AppendBit(oldBv.Get(idx));
        }
        _bm[item] = newBv;
        // The count will be updated centrally when compression finishes
    }

    public void UpdateMaxHuiCount(int newCount)
    {
        _maxHuiCount = newCount;
    }
}
