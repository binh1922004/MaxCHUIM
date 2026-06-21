using System.Collections.Generic;
using MaxCHUIM.Models;

namespace MaxCHUIM.Algorithms;

public class ChuiEntry(Itemset itemset, int support, long utility)
{
    public Itemset Itemset { get; } = itemset;
    public int Support { get; } = support;
    public long Utility { get; } = utility;
}

public class ChuiStore
{
    // Keyed by TWU of the itemset
    private readonly Dictionary<long, List<ChuiEntry>> _chuiByTwu = new();

    public void Add(Itemset itemset, int support, long utility, long twu)
    {
        if (!_chuiByTwu.TryGetValue(twu, out var bucket))
        {
            bucket = new List<ChuiEntry>();
            _chuiByTwu[twu] = bucket;
        }
        bucket.Add(new ChuiEntry(itemset, support, utility));
    }

    public bool CheckBackward(Itemset B, int suppB, long twuB)
    {
        if (!_chuiByTwu.TryGetValue(twuB, out var bucket))
        {
            return false;
        }

        int count = bucket.Count;
        for (int i = 0; i < count; i++)
        {
            var entry = bucket[i];
            if (entry.Itemset.Count > B.Count
                && entry.Support == suppB
                && entry.Itemset.IsSupersetOf(B))
            {
                return true;
            }
        }
        return false;
    }

    public List<ChuiEntry> GetAllEntries()
    {
        var result = new List<ChuiEntry>();
        foreach (var bucket in _chuiByTwu.Values)
        {
            result.AddRange(bucket);
        }
        return result;
    }

    public void Clear()
    {
        _chuiByTwu.Clear();
    }
}
