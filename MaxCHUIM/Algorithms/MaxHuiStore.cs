using System.Collections.Generic;
using MaxCHUIM.Models;

namespace MaxCHUIM.Algorithms;

public class MaxHuiEntry
{
    public Itemset Itemset { get; }
    public long Utility { get; }

    public MaxHuiEntry(Itemset itemset, long utility)
    {
        Itemset = itemset;
        Utility = utility;
    }
}

public class MaxHuiStore
{
    // Keyed by TWU of the itemset, sorted ascending
    private readonly SortedDictionary<long, List<MaxHuiEntry>> _maxHuis = new();

    public void UpdateMHUI(Itemset A, long utility, long twu)
    {
        // 1. If any existing maximal M exists with M ⊃ A -> do not add.
        // Such M must have TWU(M) <= TWU(A) = twu.
        foreach (var kvp in _maxHuis)
        {
            if (kvp.Key > twu)
            {
                break; // Since SortedDictionary is sorted ascending, we can stop
            }

            var list = kvp.Value;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = list[i];
                if (entry.Itemset.Count >= A.Count && entry.Itemset.IsSupersetOf(A))
                {
                    return; // A is subsumed by an existing maximal itemset, so do not add.
                }
            }
        }

        // 2. Otherwise add A, and remove any existing M' ⊂ A.
        // Such M' must have TWU(M') >= TWU(A) = twu.
        var keysToRemove = new List<long>();
        foreach (var kvp in _maxHuis)
        {
            if (kvp.Key < twu)
            {
                continue;
            }

            var list = kvp.Value;
            list.RemoveAll(entry => entry.Itemset.Count < A.Count && entry.Itemset.IsSubsetOf(A));
            if (list.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _maxHuis.Remove(key);
        }

        // 3. Add A to the bucket
        if (!_maxHuis.TryGetValue(twu, out var bucket))
        {
            bucket = new List<MaxHuiEntry>();
            _maxHuis[twu] = bucket;
        }
        bucket.Add(new MaxHuiEntry(A, utility));
    }

    public List<MaxHuiEntry> GetAllEntries()
    {
        var result = new List<MaxHuiEntry>();
        foreach (var bucket in _maxHuis.Values)
        {
            result.AddRange(bucket);
        }
        return result;
    }

    public void Clear()
    {
        _maxHuis.Clear();
    }
}
