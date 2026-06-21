using System.Collections.Generic;
using MaxCHUIM.Models;
using MaxCHUIM.DataStructures;

namespace MaxCHUIM.Algorithms;

public class BmChuiStore
{
    private readonly Dictionary<long, List<(ChuiEntry Entry, int BitId)>> _chuiByTwu = new();
    private readonly BmChui _bmChui = new();

    public void Add(Itemset itemset, int support, long utility, long twu)
    {
        int bitId = _bmChui.Append(itemset.Items);
        
        if (!_chuiByTwu.TryGetValue(twu, out var bucket))
        {
            bucket = new List<(ChuiEntry, int)>();
            _chuiByTwu[twu] = bucket;
        }
        bucket.Add((new ChuiEntry(itemset, support, utility), bitId));
    }

    // 3-layer pipeline from the paper (PSNonCHUB_Bitmax)
    public bool CheckBackward(Itemset B, int suppB, long twuB)
    {
        // Layer 1 - Bitmax
        var P = _bmChui.Intersect(B.Items);
        if (P.IsZero())
        {
            return false; // Fast skip
        }

        // Layer 2 - TWU hash
        if (!_chuiByTwu.TryGetValue(twuB, out var bucket))
        {
            return false;
        }

        // Layer 3 - Verification
        int count = bucket.Count;
        for (int i = 0; i < count; i++)
        {
            var (entry, bitId) = bucket[i];

            // Use bit-id from P valid
            if (!P.Get(bitId))
            {
                continue;
            }

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
            foreach (var tuple in bucket)
            {
                result.Add(tuple.Entry);
            }
        }
        return result;
    }
}
