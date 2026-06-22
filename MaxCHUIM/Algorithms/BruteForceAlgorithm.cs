using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Models;
using MaxCHUIM.DataStructures;
using MaxCHUIM.Utilities;
using MaxCHUIM.Interface;

namespace MaxCHUIM.Algorithms;

public class BruteForceAlgorithm : BaseAlgorithm
{
    private class HuiEntryInternal
    {
        public Itemset Itemset { get; }
        public int Support { get; }
        public long Utility { get; }

        public HuiEntryInternal(Itemset itemset, int support, long utility)
        {
            Itemset = itemset;
            Support = support;
            Utility = utility;
        }
    }

    private long _mu;
    private long _candidatesCount;
    private long _maxHuiChecksCount;
    private List<HuiEntryInternal> _allHuis = new();
    private Tput _tput = null!;
    private Transaction[] _txById = null!;

    public AlgorithmResult Run(QuantitativeDatabase db, long mu, AlgorithmMode mode)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _mu = mu;
        _candidatesCount = 0;
        _maxHuiChecksCount = 0;
        _allHuis.Clear();

        var rd = DatasetPreprocessor.Preprocess(db, mu);

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

        _tput = new Tput();
        _tput.Build(rd);

        var frequentItems = rd.TwuMap.Keys.ToList();
        frequentItems.Sort((a, b) =>
        {
            var twuA = rd.TwuMap[a];
            var twuB = rd.TwuMap[b];
            return twuA != twuB ? twuA.CompareTo(twuB) : a.CompareTo(b);
        });

        int itemCount = frequentItems.Count;
        for (int j = 0; j < itemCount; j++)
        {
            int aj = frequentItems[j];
            var mlJ = MpunList.BuildOneItemset(_tput, aj, _txById);

            _candidatesCount++;

            if (mlJ.Utility() >= _mu)
            {
                _allHuis.Add(new HuiEntryInternal(new Itemset(new[] { aj }), mlJ.Support(), mlJ.Utility()));
            }

            if (mlJ.Fwub() >= _mu)
            {
                var mls = new List<MpunList>();
                for (int k = j + 1; k < itemCount; k++)
                {
                    int ak = frequentItems[k];
                    var mlJK = MpunList.BuildTwoItemset(_tput, aj, ak, _txById);
                    if (mlJK.Support() > 0)
                    {
                        mls.Add(mlJK);
                    }
                }

                if (mls.Count > 0)
                {
                    FindAllHUIs(mls, new List<int> { aj });
                }
            }
        }

        // Now we have ALL HUIs in memory. Filter for MaxHUI and CHUI exactly.
        _allHuis.Sort((a, b) => b.Itemset.Count.CompareTo(a.Itemset.Count)); // Sort by length descending

        var chuis = new List<ChuiEntry>();
        var maxHuis = new List<MaxHuiEntry>();

        for (int i = 0; i < _allHuis.Count; i++)
        {
            var X = _allHuis[i];
            bool isMaxHui = true;
            bool isChui = true;
            _maxHuiChecksCount++; // Treat cross-checking as MaxHUI check

            // Only check against previously processed items (which have equal or greater length)
            for (int j = 0; j < i; j++)
            {
                var Y = _allHuis[j];
                
                if (X.Itemset.Count < Y.Itemset.Count && X.Itemset.IsSubsetOf(Y.Itemset))
                {
                    isMaxHui = false;
                    if (X.Support == Y.Support)
                    {
                        isChui = false;
                    }
                }
                
                if (!isMaxHui && !isChui) break; // Fully subsumed
            }

            if (isChui) chuis.Add(new ChuiEntry(X.Itemset, X.Support, X.Utility));
            if (isMaxHui) maxHuis.Add(new MaxHuiEntry(X.Itemset, X.Utility));
        }

        watch.Stop();

        return new AlgorithmResult
        {
            CHUIs = chuis,
            MaxHUIs = maxHuis,
            Runtime = watch.Elapsed,
            CandidatesCount = _candidatesCount,
            MaxHuiChecksCount = _maxHuiChecksCount
        };
    }

    private void FindAllHUIs(List<MpunList> mpunLists, List<int> prefix)
    {
        int count = mpunLists.Count;
        for (int i = 0; i < count; i++)
        {
            var mlX = mpunLists[i];
            
            var newPrefix = new List<int>(prefix.Count + 1);
            newPrefix.AddRange(prefix);
            newPrefix.Add(mlX.Item);

            _candidatesCount++;

            if (mlX.Utility() >= _mu)
            {
                _allHuis.Add(new HuiEntryInternal(new Itemset(newPrefix), mlX.Support(), mlX.Utility()));
            }

            if (mlX.Fwub() >= _mu)
            {
                var nextLists = new List<MpunList>();
                for (int j = i + 1; j < count; j++)
                {
                    var mlY = mpunLists[j];
                    var mlXY = MpunList.JoinLists(mlX, mlY);
                    
                    if (mlXY.Support() > 0)
                    {
                        nextLists.Add(mlXY);
                    }
                }

                if (nextLists.Count > 0)
                {
                    FindAllHUIs(nextLists, newPrefix);
                }
            }
        }
    }
}
