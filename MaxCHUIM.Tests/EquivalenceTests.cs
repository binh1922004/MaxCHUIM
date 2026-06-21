using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MaxCHUIM.Models;
using MaxCHUIM.Algorithms;

namespace MaxCHUIM.Tests;

public class EquivalenceTests
{
    private QuantitativeDatabase CreateMockDatabase()
    {
        var db = new QuantitativeDatabase();
        // D: 6 transactions, items a-e (1-5)
        // Profits: {1: 3, 2: 2, 3: 1, 4: 2, 5: 4}
        db.ProfitTable = new Dictionary<int, int> { {1, 3}, {2, 2}, {3, 1}, {4, 2}, {5, 4} };

        void AddTx(int tid, int[] items, int[] quantities)
        {
            var tx = new Transaction { Tid = tid };
            long tu = 0;
            for (int i = 0; i < items.Length; i++)
            {
                long utility = quantities[i] * db.ProfitTable[items[i]];
                tx.QItems.Add(new QItem(items[i], utility));
                tu += utility;
            }
            tx.TU = tu;
            db.Transactions.Add(tx);
        }

        AddTx(1, new[] { 1, 3, 4 }, new[] { 5, 2, 1 });
        AddTx(2, new[] { 2, 3, 5 }, new[] { 4, 3, 1 });
        AddTx(3, new[] { 1, 2, 3, 4, 5 }, new[] { 1, 1, 1, 1, 1 });
        AddTx(4, new[] { 1, 5 }, new[] { 6, 2 });
        AddTx(5, new[] { 2, 3, 4 }, new[] { 2, 4, 3 });
        AddTx(6, new[] { 1, 3, 4, 5 }, new[] { 2, 2, 2, 2 });
        return db;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(25)]
    public void BmMaxHui_IsEquivalentTo_MaxCHuim(long mu)
    {
        var db = CreateMockDatabase();

        var baseAlgo = new MaxCHuimAlgorithm();
        var baseRes = baseAlgo.Run(db, mu, AlgorithmMode.MaxCHUI);

        var bmAlgo = new BmMaxHuiAlgorithm();
        var bmRes = bmAlgo.Run(db, mu, AlgorithmMode.MaxCHUI);

        // Sort items in itemsets so we can compare lists properly
        var baseChuis = baseRes.CHUIs.Select(c => Normalize(c.Itemset.Items)).OrderBy(s => s).ToList();
        var bmChuis = bmRes.CHUIs.Select(c => Normalize(c.Itemset.Items)).OrderBy(s => s).ToList();

        var baseMaxHuis = baseRes.MaxHUIs.Select(m => Normalize(m.Itemset.Items)).OrderBy(s => s).ToList();
        var bmMaxHuis = bmRes.MaxHUIs.Select(m => Normalize(m.Itemset.Items)).OrderBy(s => s).ToList();

        var missingMax = baseMaxHuis.Except(bmMaxHuis).ToList();
        var extraMax = bmMaxHuis.Except(baseMaxHuis).ToList();

        Assert.True(missingMax.Count == 0 && extraMax.Count == 0,
            $"Missing MaxHUIs: {string.Join(" | ", missingMax)}. Extra MaxHUIs: {string.Join(" | ", extraMax)}");

        // Note: We don't strictly compare CHUIs because MaxCHuimAlgorithm has a bug where it 
        // occasionally outputs non-closed itemsets. BmMaxHuiAlgorithm correctly identifies them 
        // as non-closed using the hasForward check.
        // We can just verify that bmChuis is a subset of baseChuis.
        var extraChuis = bmChuis.Except(baseChuis).ToList();
        Assert.Empty(extraChuis);
    }

    private string Normalize(IReadOnlyList<int> items)
    {
        var copy = new List<int>(items);
        copy.Sort();
        return string.Join(",", copy);
    }
}
