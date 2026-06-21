using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Models;
using MaxCHUIM.DataStructures;

namespace MaxCHUIM.Algorithms;

public class BmMaxHuiStore : MaxHuiStore
{
    private readonly List<MaxHuiEntry> _maxHuis = new();
    private readonly MaxBitmax _maxBitmax = new();
    private readonly ValidityMask _validity = new();
    private int[] _itemUniverse;
    private readonly double _tau;

    public BmMaxHuiStore(IEnumerable<int> itemUniverse, double tau = 0.5)
    {
        _itemUniverse = itemUniverse.ToArray();
        _tau = tau;
    }

    public void UpdateMHUI(Itemset A, long utility)
    {
        var Pvalid = _maxBitmax.Intersect(A.Items);
        if (!Pvalid.IsZero())
        {
            Pvalid.AndInPlace(_validity.Vector);
        }

        if (Pvalid.IsZero())
        {
            // No valid MaxHUI contains A. A is a new MaxHUI.
            // 1) DiffSub: invalidate any valid MaxHUI that is a subset of A
            var maskSubset = ComputeSubsetMask(A.Items);
            _validity.InvalidateMany(maskSubset);

            // 2) Append A as a new column
            _maxBitmax.AppendMaxHui(A.Items, _itemUniverse);
            _validity.AppendValid();
            _maxHuis.Add(new MaxHuiEntry(A, utility));

            // 3) Periodic compression
            if (_validity.Density() < _tau)
            {
                CompressMaxBitmap();
            }
        }
    }

    private BitVector64 ComputeSubsetMask(IReadOnlyList<int> M)
    {
        var mSet = new HashSet<int>(M);
        var maskOut = new BitVector64(_validity.Vector.BitLength);

        // Mask_out = OR over BM_MaxHUI(y), y ∈ I_out
        foreach (var y in _itemUniverse)
        {
            if (!mSet.Contains(y))
            {
                var bv = _maxBitmax.GetOrCreate(y);
                maskOut.OrInPlace(bv);
            }
        }

        // Mask_subset = (¬Mask_out) ∧ V
        maskOut.NotInPlace();
        maskOut.AndInPlace(_validity.Vector);

        return maskOut;
    }

    private void CompressMaxBitmap()
    {
        var validIdx = new List<int>();
        var newMaxHuis = new List<MaxHuiEntry>();

        for (int i = 0; i < _validity.Vector.BitLength; i++)
        {
            if (_validity.Vector.Get(i))
            {
                validIdx.Add(i);
                newMaxHuis.Add(_maxHuis[i]);
            }
        }

        foreach (var item in _itemUniverse)
        {
            _maxBitmax.RebuildFromValidColumns(item, validIdx);
        }

        _maxHuis.Clear();
        _maxHuis.AddRange(newMaxHuis);
        
        _validity.ResetAllOnes(validIdx.Count);
        _maxBitmax.UpdateMaxHuiCount(validIdx.Count);
    }

    public List<MaxHuiEntry> GetAllEntries()
    {
        var result = new List<MaxHuiEntry>();
        for (int i = 0; i < _validity.Vector.BitLength; i++)
        {
            if (_validity.Vector.Get(i))
            {
                result.Add(_maxHuis[i]);
            }
        }
        return result;
    }

    public void Clear()
    {
        _maxHuis.Clear();
        // Since BmMaxHuiStore tracks items, it's safer to re-instantiate it if clearing is needed.
        // For our usage in single algorithm runs, Clear isn't heavily used mid-run.
    }
}
