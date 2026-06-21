using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Models;
using MaxCHUIM.DataStructures;
using MaxCHUIM.Interface;
using MaxCHUIM.Utilities;

namespace MaxCHUIM.Algorithms;

public class BmMaxHuiAlgorithm : BaseAlgorithm
{
    private long _mu;
    private AlgorithmMode _mode;
    private Tput _tput = null!;
    private Transaction[] _txById = null!;
    private int _newms;
    private long _candidatesCount;

    private BmChuiStore _chuiStore = null!;
    private BmMaxHuiStore _maxHuiStore = null!;

    public AlgorithmResult Run(QuantitativeDatabase db, long mu, AlgorithmMode mode)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _mu = mu;
        _mode = mode;
        _candidatesCount = 0;

        // 2. Preprocess database
        var rd = DatasetPreprocessor.Preprocess(db, mu);
        _newms = rd.Newms;

        // Ordered list of frequent items (by ascending TWU, matching TPUT structure)
        var frequentItems = rd.TwuMap.Keys.ToList();
        frequentItems.Sort((a, b) =>
        {
            var twuA = rd.TwuMap[a];
            var twuB = rd.TwuMap[b];
            return twuA != twuB ? twuA.CompareTo(twuB) : a.CompareTo(b);
        });

        // Initialize stores
        _chuiStore = new BmChuiStore();
        _maxHuiStore = new BmMaxHuiStore(frequentItems);

        // Build O(1) transaction lookup
        var maxTid = 0;
        var txCount = rd.Transactions.Count;
        for (var i = 0; i < txCount; i++)
        {
            var tid = rd.Transactions[i].Tid;
            if (tid > maxTid) maxTid = tid;
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

        // 4. Outer loop over items aj
        var itemCount = frequentItems.Count;
        for (var j = 0; j < itemCount; j++)
        {
            var aj = frequentItems[j];
            var itemsetA = new Itemset([aj]);

            // Build 1-itemset MPUN-list
            var mlJ = MpunList.BuildOneItemset(_tput, aj, _txById);
            long twuA = mlJ.ComputeTwu(_tput, _txById);

            // SPWUB check
            if (mlJ.Fwub() < _mu)
            {
                // Is closed check isn't fully possible here (no extensions), just try to update
                UpdateBmMaxCHUI(itemsetA, mlJ.Utility(), twuA, mlJ.Support(), true);
                continue;
            }

            // newms opt
            if (mlJ.Support() < _newms)
            {
                continue;
            }

            _candidatesCount++;

            // Build 2-itemsets MPUN-lists
            var mls = new List<MpunList>();
            for (int k = j + 1; k < itemCount; k++)
            {
                int ak = frequentItems[k];
                var mlJK = MpunList.BuildTwoItemset(_tput, aj, ak, _txById);
                mls.Add(mlJK);
            }

            bool hasForward = mls.Any(ml => ml.Support() == mlJ.Support());
            bool backward = _chuiStore.CheckBackward(itemsetA, mlJ.Support(), twuA);
            bool isClosed = !hasForward && !backward;

            UpdateBmMaxCHUI(itemsetA, mlJ.Utility(), twuA, mlJ.Support(), isClosed);

            FindBmMaxCHUI(mls, [aj]);
        }

        watch.Stop();

        var result = new AlgorithmResult
        {
            CHUIs = _chuiStore.GetAllEntries(),
            MaxHUIs = _mode == AlgorithmMode.MaxCHUI ? _maxHuiStore.GetAllEntries() : new List<MaxHuiEntry>(),
            Runtime = watch.Elapsed,
            CandidatesCount = _candidatesCount
        };

        return result;
    }

    private void FindBmMaxCHUI(List<MpunList> mls, List<int> prefix)
    {
        int mlsCount = mls.Count;
        for (int j = 0; j < mlsCount; j++)
        {
            var mlJ = mls[j];
            int itemJ = mlJ.Item;

            var AList = new List<int>(prefix.Count + 1);
            AList.AddRange(prefix);
            AList.Add(itemJ);
            var A = new Itemset(AList);

            long twuA = mlJ.ComputeTwu(_tput, _txById);

            if (mlJ.Fwub() < _mu)
            {
                UpdateBmMaxCHUI(A, mlJ.Utility(), twuA, mlJ.Support(), true);
                continue;
            }

            if (mlJ.Support() < _newms)
            {
                continue;
            }

            if (mlJ.PruningNonMCBr)
            {
                continue;
            }

            _candidatesCount++;

            var extensionLists = new List<MpunList>();
            for (int k = j + 1; k < mlsCount; k++)
            {
                var mlK = mls[k];
                var mlJK = MpunList.JoinLists(mlJ, mlK);
                extensionLists.Add(mlJK);
            }

            // Apply LPSNonCHUB mark for the sibling extensions
            int extCount = extensionLists.Count;
            for (int x = 0; x < extCount; x++)
            {
                var mlC = extensionLists[x];
                for (int y = x + 1; y < extCount; y++)
                {
                    var mlB = extensionLists[y];
                    var mlS = MpunList.JoinLists(mlC, mlB);
                    if (mlS.Support() == mlB.Support())
                    {
                        mlB.PruningNonMCBr = true;
                    }
                }
            }

            bool hasForward = extensionLists.Any(ml => ml.Support() == mlJ.Support());
            bool backward = _chuiStore.CheckBackward(A, mlJ.Support(), twuA);
            bool isClosed = !hasForward && !backward;

            UpdateBmMaxCHUI(A, mlJ.Utility(), twuA, mlJ.Support(), isClosed);

            // Important: Recurse into surviving extensions regardless of isClosed
            var survivingExtensions = new List<MpunList>();
            for (int k = 0; k < extCount; k++)
            {
                var mlJK = extensionLists[k];
                if (mlJK.Support() >= _newms && mlJK.Fwub() >= _mu && !mlJK.PruningNonMCBr)
                {
                    survivingExtensions.Add(mlJK);
                }
                else if (mlJK.Fwub() < _mu)
                {
                    var extItemList = new List<int>(AList.Count + 1);
                    extItemList.AddRange(AList);
                    extItemList.Add(mlJK.Item);
                    var extItemset = new Itemset(extItemList);
                    UpdateBmMaxCHUI(extItemset, mlJK.Utility(), mlJK.ComputeTwu(_tput, _txById), mlJK.Support(), true);
                }
            }

            if (survivingExtensions.Count > 0)
            {
                FindBmMaxCHUI(survivingExtensions, AList);
            }
        }
    }

    private void UpdateBmMaxCHUI(Itemset A, long utility, long twu, int support, bool isClosed)
    {
        if (utility >= _mu)
        {
            if (isClosed)
            {
                _chuiStore.Add(A, support, utility, twu);
            }

            if (_mode == AlgorithmMode.MaxCHUI)
            {
                _maxHuiStore.UpdateMHUI(A, utility);
            }
        }
    }
}
