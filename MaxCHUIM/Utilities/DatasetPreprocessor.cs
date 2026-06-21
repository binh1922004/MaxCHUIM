using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Models;

namespace MaxCHUIM.Utilities;

public record ReducedDatabase(
    List<Transaction> Transactions,
    Dictionary<int, long> TwuMap,
    long MaxTwu,
    int Newms
);

public static class DatasetPreprocessor
{
    public static ReducedDatabase Preprocess(QuantitativeDatabase db, long mu)
    {
        // 1. First scan: compute TWU(aj) for every item.
        var twuMap = new Dictionary<int, long>();
        foreach (var tx in db.Transactions)
        {
            // Use HashSet to avoid counting duplicate items in a transaction (though SPMF transactions shouldn't have duplicates)
            var uniqueItems = tx.QItems.Select(q => q.Item).Distinct();
            foreach (var item in uniqueItems)
            {
                twuMap[item] = twuMap.GetValueOrDefault(item) + tx.TU;
            }
        }

        // 2. Remove items where TWU(aj) < mu.
        var frequentItems = twuMap.Where(kvp => kvp.Value >= mu).Select(kvp => kvp.Key).ToHashSet();

        // 3. Remove empty transactions, filter items, and sort items inside each transaction by ascending TWU.
        var reducedTransactions = new List<Transaction>();

        foreach (var tx in db.Transactions)
        {
            var filteredQItems = tx.QItems
                .Where(qi => frequentItems.Contains(qi.Item))
                .ToList();

            if (filteredQItems.Count > 0)
            {
                // Sort items inside transaction by ascending TWU (≺twu)
                filteredQItems.Sort((a, b) => CompareItems(a.Item, b.Item));

                // Recalculate transaction utility for the filtered items?
                // Wait! In the paper or standard algorithms, does TU of a transaction change after filtering?
                // Actually, in some papers the TU remains the original TU, or it is updated to the sum of remaining items' utilities.
                // According to Definition 2 and the standard HUIM prefix tree construction:
                // Transaction utility is typically kept as the sum of utilities of the remaining items, or the original TU.
                // Let's check Section 4.3.1: "frequent items are sorted ... and transactions are rebuilt".
                // In standard algorithms like EFIM and CHUI-Miner, the transaction utility TU of a transaction in the reduced database D'
                // is the sum of utilities of the remaining frequent items in that transaction.
                // Let's compute the new TU as the sum of the remaining items' utilities.
                long newTu = filteredQItems.Sum(qi => qi.Utility);
                
                // don't need to recalculate TU if we want to keep the original TU, but for the reduced database, it's often better to have the new TU.
                reducedTransactions.Add(new Transaction
                {
                    Tid = tx.Tid,
                    QItems = filteredQItems,
                    TU = newTu
                });
            }
        }

        // 4. Sort transactions by descending TWU order of items.
        // We compare transactions lexicographically using the ≺twu item order.
        // Descending order means higher TWU items/prefixes come first.
        int CompareTransactions(Transaction t1, Transaction t2)
        {
            var len = Math.Min(t1.QItems.Count, t2.QItems.Count);
            for (var i = 0; i < len; i++)
            {
                int cmp = CompareItems(t1.QItems[i].Item, t2.QItems[i].Item);
                if (cmp != 0)
                {
                    return cmp; // Descending order 
                }
            }
            return t1.QItems.Count.CompareTo(t2.QItems.Count); // Descending order of lengths
        }

        reducedTransactions.Sort(CompareTransactions);

        // 5. Compute maxTWU and newms = max(1, ⌈mu / maxTWU⌉) (Remark 1.a).
        var maxTwu = frequentItems.Select(item => twuMap[item]).Prepend(0).Max();

        var newms = 1;
        if (maxTwu > 0)
        {
            newms = (int)Math.Max(1, Math.Ceiling((double)mu / maxTwu));
        }

        // Keep only frequent items in the twuMap
        var reducedTwuMap = twuMap.Where(kvp => frequentItems.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var rd =  new ReducedDatabase(reducedTransactions, reducedTwuMap, maxTwu, newms);
        foreach (var item in reducedTransactions)
        {
            Console.WriteLine($"Tid: {item.Tid}, TU: {item.TU}, QItems: [{string.Join(", ", item.QItems.Select(q => $"(Item: {q.Item}, Util: {q.Utility})"))}]");
        }

        return rd;
        // Define item comparison helper
        int CompareItems(int itemA, int itemB)
        {
            var twuA = twuMap[itemA];
            var twuB = twuMap[itemB];
            if (twuA != twuB)
            {
                return -twuA.CompareTo(twuB); // Descending TWU
            }
            return -itemA.CompareTo(itemB); // Stable tie-break (descending)
        }
    }
}


// 360
// 230 
// 150
// 200
// 101
// 85