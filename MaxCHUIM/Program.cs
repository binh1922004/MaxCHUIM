using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Algorithms;
using MaxCHUIM.DataStructures;
using MaxCHUIM.Interface;
using MaxCHUIM.Models;
using MaxCHUIM.Utilities;

class Program
{
    static void Main(string[] args)
    {
        const string huiFile = "/Users/mac/BINH/NCKH/Dataset/HUI/mushroom.hui";
        const string proFile = "/Users/mac/BINH/NCKH/Dataset/PRO/mushroom.pro";


        // Load database using the new HuiProReader
        var db = HuiProReader.Read(huiFile, proFile);

        var threshHold = 0.9 * db.Transactions.Count;
        var mu = (long)threshHold;
        // Preprocess database manually to build and verify MPUN lists
        var rd = DatasetPreprocessor.Preprocess(db, mu);
        
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
        var txById = new Transaction[maxTid + 1];
        for (var i = 0; i < txCount; i++)
        {
            var tx = rd.Transactions[i];
            txById[tx.Tid] = tx;
        }

        var tput = new Tput();
        tput.Build(rd);
        
        Console.WriteLine("========================================");
        var res1 = RunAlgorithm("MaxCHuim", db, mu, AlgorithmMode.MaxCHUI);
        
        Console.WriteLine("========================================");
        var res2 = RunAlgorithm("BmMaxHui", db, mu, AlgorithmMode.MaxCHUI);
        
        Console.WriteLine("========================================");
        var res3 = RunAlgorithm("BruteForce", db, mu, AlgorithmMode.MaxCHUI);
        
        VerifyAndSortResults(db, res1);
        VerifyAndSortResults(db, res2);
        VerifyAndSortResults(db, res3);

        Console.WriteLine("\n[1] Compare BmMaxHui against Ground Truth (BruteForce):");
        CompareMaxHUIs(res3.MaxHUIs, res2.MaxHUIs); // list1=BruteForce, list2=BmMaxHui
        
        Console.WriteLine("\n[2] Compare MaxCHuim against Ground Truth (BruteForce):");
        CompareMaxHUIs(res3.MaxHUIs, res1.MaxHUIs); // list1=BruteForce, list2=MaxCHuim
    }

    static void VerifyAndSortResults(QuantitativeDatabase db, AlgorithmResult res)
    {
        res.MaxHUIs.Sort((a, b) =>
        {
            var cmp = a.Itemset.Count.CompareTo(b.Itemset.Count);
            if (cmp != 0) return cmp;
            for (int i = 0; i < a.Itemset.Count; i++)
            {
                var c = a.Itemset.Items[i].CompareTo(b.Itemset.Items[i]);
                if (c != 0) return c;
            }
            return 0;
        });

        int utilityMismatches = 0;
        foreach (var mh in res.MaxHUIs)
        {
            long trueUtil = CalculateExactUtility(db, mh.Itemset.Items);
            if (trueUtil != mh.Utility)
            {
                // Console.WriteLine($"[WARNING] Utility mismatch for {mh.Itemset}: True={trueUtil}, Reported={mh.Utility}");
                utilityMismatches++;
            }
        }
        
        if (utilityMismatches > 0)
        {
            Console.WriteLine($"Found {utilityMismatches} itemsets with incorrectly reported utility!");
        }
        else
        {
            Console.WriteLine("All reported utilities are EXACTLY correct.");
        }
    }

    static void CompareMaxHUIs(List<MaxHuiEntry> list1, List<MaxHuiEntry> list2)
    {
        Console.WriteLine("\n--- Comparing MaxHUI sets ---");
        int exactMatches = 0;
        int list2ProperSubsetOfList1 = 0;

        foreach (var m2 in list2)
        {
            bool matchedExactly = false;
            foreach (var m1 in list1)
            {
                if (m2.Itemset.Equals(m1.Itemset))
                {
                    exactMatches++;
                    matchedExactly = true;
                    break;
                }
            }

            if (!matchedExactly)
            {
                foreach (var m1 in list1)
                {
                    if (m2.Itemset.IsSubsetOf(m1.Itemset) && m2.Itemset.Count < m1.Itemset.Count)
                    {
                        list2ProperSubsetOfList1++;
                        break;
                    }
                }
            }
        }
        Console.WriteLine($"Number of EXACT MATCHES between both algorithms: {exactMatches}");
        Console.WriteLine($"Number of BmMaxHui MaxHUIs that are strictly smaller subsets of MaxCHuim: {list2ProperSubsetOfList1}");

        int list1ProperSubsetOfList2 = 0;
        int missingAndNotSubset = 0;
        foreach (var m1 in list1)
        {
            bool matchedExactly = false;
            foreach (var m2 in list2)
            {
                if (m1.Itemset.Equals(m2.Itemset))
                {
                    matchedExactly = true;
                    break;
                }
            }

            if (!matchedExactly)
            {
                bool isProperSubset = false;
                foreach (var m2 in list2)
                {
                    if (m1.Itemset.IsSubsetOf(m2.Itemset) && m1.Itemset.Count < m2.Itemset.Count)
                    {
                        isProperSubset = true;
                        break;
                    }
                }
                if (isProperSubset) list1ProperSubsetOfList2++;
                else missingAndNotSubset++;
            }
        }
        Console.WriteLine($"Number of MaxCHuim MaxHUIs that are strictly smaller subsets of BmMaxHui: {list1ProperSubsetOfList2}");
        Console.WriteLine($"Number of MaxCHuim MaxHUIs entirely missing and NOT subsets: {missingAndNotSubset}");
    }

    static AlgorithmResult RunAlgorithm(string algorithm, QuantitativeDatabase db, long mu, AlgorithmMode mode)
    {
        BaseAlgorithm algo = null;
        if (algorithm == "MaxCHuim")
        {
            algo = new MaxCHuimAlgorithm();
        }
        else if (algorithm == "BmMaxHui")
        {
            algo = new BmMaxHuiAlgorithm();
        }
        else if (algorithm == "BruteForce")
        {
            algo = new BruteForceAlgorithm();
        }
        
        Console.WriteLine($"Algorithm: {algorithm}, Mode: {mode}, mu: {mu}");

        // Mine both CHUIs and MaxHUIs
        var result = algo.Run(db, mu, AlgorithmMode.MaxCHUI);

        Console.WriteLine("\n--- Mined Closed High Utility Itemsets (CHUIs) ---");
        // foreach (var chui in result.CHUIs)
        // {
        //     Console.WriteLine($"Itemset: {chui.Itemset}, Utility: {chui.Utility}, Support: {chui.Support}");
        // }

        Console.WriteLine("\n--- Mined Maximal High Utility Itemsets (MaxHUIs) ---");
        // foreach (var maxHui in result.MaxHUIs)
        // {
        //     Console.WriteLine($"Itemset: {maxHui.Itemset}, Utility: {maxHui.Utility}");
        // }

        Console.WriteLine($"\nRuntime: {result.Runtime.TotalMilliseconds} ms");
        Console.WriteLine($"ClosedHUIs Found: {result.CHUIs.Count} - MaxHUIs Found: {result.MaxHUIs.Count}");
        Console.WriteLine($"Candidate Itemsets Evaluated: {result.CandidatesCount}");
        Console.WriteLine($"MaxHui Checks Performed: {result.MaxHuiChecksCount}");
        
        ValidateMaxHUIs(result.MaxHUIs);

        int fakeCount = 0;
        foreach (var mh in result.MaxHUIs)
        {
            long trueUtil = CalculateExactUtility(db, mh.Itemset.Items);
            if (trueUtil < mu)
            {
                fakeCount++;
            }
        }
        Console.WriteLine($"Fake HUIs (Utility < mu): {fakeCount}");
        return result;
    }

    static long CalculateExactUtility(QuantitativeDatabase db, IReadOnlyList<int> items)
    {
        long totalUtility = 0;
        foreach (var tx in db.Transactions)
        {
            bool containsAll = true;
            long txUtility = 0;
            foreach (var item in items)
            {
                long itemUtil = tx.GetItemUtility(item);
                if (itemUtil == 0)
                {
                    containsAll = false;
                    break;
                }
                txUtility += itemUtil;
            }
            if (containsAll)
            {
                totalUtility += txUtility;
            }
        }
        return totalUtility;
    }

    static string GetNodePath(Tput tput, int nid)
    {
        var node = tput.NodesById[nid];
        var path = new List<int>();
        var curr = node;
        while (curr != null && curr.Item != -1)
        {
            path.Add(curr.Item);
            curr = curr.ParentLink;
        }
        path.Reverse();
        return "[" + string.Join(", ", path) + "]";
    }

    static void ValidateMaxHUIs(List<MaxHuiEntry> maxHuis)
    {
        Console.WriteLine("Validating MaxHUIs...");
        bool isValid = true;
        for (int i = 0; i < maxHuis.Count; i++)
        {
            for (int j = i + 1; j < maxHuis.Count; j++)
            {
                var itemsetA = maxHuis[i].Itemset;
                var itemsetB = maxHuis[j].Itemset;
                
                if (itemsetA.IsSubsetOf(itemsetB))
                {
                    Console.WriteLine($"[FAIL] Itemset {itemsetA} is a subset of {itemsetB}");
                    isValid = false;
                }
                else if (itemsetB.IsSubsetOf(itemsetA))
                {
                    Console.WriteLine($"[FAIL] Itemset {itemsetB} is a subset of {itemsetA}");
                    isValid = false;
                }
            }
        }

        if (isValid)
        {
            Console.WriteLine("[PASS] All MaxHUIs are truly maximal.");
        }
        else
        {
            Console.WriteLine("[FAIL] Validation failed! Some MaxHUIs are not maximal.");
        }
    }
}