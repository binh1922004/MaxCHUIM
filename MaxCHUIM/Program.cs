using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Algorithms;
using MaxCHUIM.DataStructures;
using MaxCHUIM.Models;
using MaxCHUIM.Utilities;

class Program
{
    static void Main(string[] args)
    {
        string huiFile = "/Users/mac/BINH/NCKH/CODE/HUI/MaxCHUIM/MaxCHUIM/example.hui";
        string proFile = "/Users/mac/BINH/NCKH/CODE/HUI/MaxCHUIM/MaxCHUIM/example.pro";
        long mu = 370;

        Console.WriteLine($"Running MaxC-HUIM on '{huiFile}' & '{proFile}' with mu = {mu}...");

        // Load database using the new HuiProReader
        var db = HuiProReader.Read(huiFile, proFile);

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

        var algo = new MaxCHuimAlgorithm();
        
        // Mine both CHUIs and MaxHUIs
        var result = algo.Run(db, mu, AlgorithmMode.MaxCHUI);

        Console.WriteLine("\n--- Mined Closed High Utility Itemsets (CHUIs) ---");
        foreach (var chui in result.CHUIs)
        {
            Console.WriteLine($"Itemset: {chui.Itemset}, Utility: {chui.Utility}, Support: {chui.Support}");
        }

        Console.WriteLine("\n--- Mined Maximal High Utility Itemsets (MaxHUIs) ---");
        foreach (var maxHui in result.MaxHUIs)
        {
            Console.WriteLine($"Itemset: {maxHui.Itemset}, Utility: {maxHui.Utility}");
        }

        Console.WriteLine($"\nRuntime: {result.Runtime.TotalMilliseconds} ms");
        Console.WriteLine($"Candidate Itemsets Evaluated: {result.CandidatesCount}");
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
}