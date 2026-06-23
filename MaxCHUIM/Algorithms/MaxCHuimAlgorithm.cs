using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Models;
using MaxCHUIM.DataStructures;
using MaxCHUIM.Interface;
using MaxCHUIM.Utilities;

namespace MaxCHUIM.Algorithms;

public enum AlgorithmMode
{
    CHUI,
    MaxCHUI
}

public class MaxCHuimAlgorithm : BaseAlgorithm
{
    private long _mu;
    private AlgorithmMode _mode;
    private Tput _tput = null!;
    private Transaction[] _txById = null!;
    private int _newms;
    private long _candidatesCount;
    private long _maxHuiChecksCount;

    private readonly ChuiStore _chuiStore = new();
    private readonly MaxHuiStore _maxHuiStore = new();

    public AlgorithmResult Run(QuantitativeDatabase db, long mu, AlgorithmMode mode)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _mu = mu;
        _mode = mode;
        _candidatesCount = 0;
        _maxHuiChecksCount = 0;
        _chuiStore.Clear();
        _maxHuiStore.Clear();

        // 2. Preprocess database
        var rd = DatasetPreprocessor.Preprocess(db, mu);
        _newms = rd.Newms;

        // Build O(1) transaction lookup
        var maxTid = 0;
        var txCount = rd.Transactions.Count;
        for (var i = 0; i < txCount; i++)
        {
            var tid = rd.Transactions[i].Tid;
            if (tid > maxTid)
            {
                maxTid = tid;
            }
        }
        _txById = new Transaction[maxTid + 1];
        for (var i = 0; i < txCount; i++)
        {
            var tx = rd.Transactions[i];
            _txById[tx.Tid] = tx;
        }

        // 3. Build TPUT
        _tput = new Tput();
        _tput.Build(rd);

        // Ordered list of frequent items (by ascending TWU, matching TPUT structure)
        var frequentItems = rd.TwuMap.Keys.ToList();
        frequentItems.Sort((a, b) =>
        {
            var twuA = rd.TwuMap[a];
            var twuB = rd.TwuMap[b];
            return twuA != twuB ? twuA.CompareTo(twuB) : a.CompareTo(b); // Stable tie-break
        });

        // 4. Outer loop over items aj (ordered by ≺twu)
        var itemCount = frequentItems.Count;
        for (var j = 0; j < itemCount; j++)
        {
            var aj = frequentItems[j];

            // Build 1-itemset MPUN-list
            var mlJ = MpunList.BuildOneItemset(_tput, aj, _txById);

            // SPWUB check
            if (mlJ.Fwub() < _mu)
            {
                // Update if HUI
                Update(new Itemset(new[] { aj }), mlJ.Utility(), mlJ.ComputeTwu(_tput, _txById), mlJ.Support());
                continue;
            }

            // newms opt
            if (mlJ.Support() < _newms)
            {
                continue;
            }

            _candidatesCount++;

            // Build 2-itemsets MPUN-lists {aj⊕ak | k ≻twu j}
            var mls = new List<MpunList>();
            for (int k = j + 1; k < itemCount; k++)
            {
                int ak = frequentItems[k];
                var mlJK = MpunList.BuildTwoItemset(_tput, aj, ak, _txById);
                mls.Add(mlJK);
            }

            var cnt = mls.Count(ml => ml.Support() == mlJ.Support());
            UpdateMaxCHUI(new Itemset([aj]), mls, cnt, mlJ.Utility(), mlJ.ComputeTwu(_tput, _txById), mlJ.Support());
            
            if (cnt < mls.Count)
            {
                FindMaxCHUI(mls, [aj]);
            }
        }

        watch.Stop();

        var result = new AlgorithmResult
        {
            CHUIs = _chuiStore.GetAllEntries(),
            MaxHUIs = _mode == AlgorithmMode.MaxCHUI ? _maxHuiStore.GetAllEntries() : new List<MaxHuiEntry>(),
            Runtime = watch.Elapsed,
            CandidatesCount = _candidatesCount,
            MaxHuiChecksCount = _maxHuiChecksCount
        };

        return result;
    }

    private void FindMaxCHUI(List<MpunList> mls, List<int> prefix)
    {
        int mlsCount = mls.Count;
        for (int j = 0; j < mlsCount; j++)
        {
            var mlJ = mls[j];
            int itemJ = mlJ.Item;
            
            // 1. A = prefix ⊕ MLj.item
            var AList = new List<int>(prefix.Count + 1);
            AList.AddRange(prefix);
            AList.Add(itemJ);
            var A = new Itemset(AList);

            // Compute TWU of A
            long twuA = mlJ.ComputeTwu(_tput, _txById);

            // 2. If fwub(A) < mu -> Update(A) then return
            if (mlJ.Fwub() < _mu)
            {
                Update(A, mlJ.Utility(), twuA, mlJ.Support());
                continue;
            }

            // 3. If supp(A) < newms -> skip
            if (mlJ.Support() < _newms)
            {
                continue;
            }

            // 4. If MLj.PruningNonMCBr == true -> skip (LPSNonCHUB)
            if (mlJ.PruningNonMCBr)
            {
                continue;
            }

            // 5. If CheckBackward(A) == true -> skip (PSNonCHUB)
            if (_chuiStore.CheckBackward(A, mlJ.Support(), twuA))
            {
                continue;
            }

            _candidatesCount++;

            // 6. Build extension MPUN-lists MLjk for each later MLk
            var extensionLists = new List<MpunList>();
            for (int k = j + 1; k < mlsCount; k++)
            {
                var mlK = mls[k];
                var mlJK = MpunList.JoinLists(mlJ, mlK);
                if (mlJK.Support() == mlK.Support())
                {
                    mlK.PruningNonMCBr = true;
                }
                extensionLists.Add(mlJK);
            }

            var cnt = extensionLists.Count(ml => ml.Support() == mlJ.Support());
            // 7. UpdateMaxCHUI
            Update(A, mlJ.Utility(), twuA, mlJ.Support());
            if (cnt < mls.Count)
            {
                FindMaxCHUI(extensionLists, AList);
            }
        }
    }

    private void UpdateMaxCHUI(Itemset A, List<MpunList> mls, int cnt, long utilityA, long twuA, int supportA)
    {
        if (cnt == 0)
        {
            Update(A, utilityA, twuA, supportA);
        }
        else if (cnt == mls.Count)
        {
            var listB = new List<int>() { A.Items[0] };
            var utilityB = mls.Sum(mpunList => mpunList.Utility()) + utilityA;
            listB.AddRange(mls.Select(mpunList => mpunList.Item));
            var itemsetB = new Itemset(listB);
            Update(itemsetB, utilityB, twuA, supportA);
        }
    }
    private void Update(Itemset A, long utility, long twu, int support)
    {
        if (utility >= _mu)
        {
            _chuiStore.Add(A, support, utility, twu);
            if (_mode == AlgorithmMode.MaxCHUI)
            {
                _maxHuiChecksCount++;
                _maxHuiStore.UpdateMHUI(A, utility, twu);
            }
        }
    }
}
