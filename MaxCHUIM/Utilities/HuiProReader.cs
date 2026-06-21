using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaxCHUIM.Models;

namespace MaxCHUIM.Utilities;

public static class HuiProReader
{
    public static QuantitativeDatabase Read(string huiPath, string proPath)
    {
        // 1. Read profits from .pro file
        var profits = new List<double> { -1 };
        using (var reader = new StreamReader(proPath))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                if (double.TryParse(line, out double profit))
                {
                    profits.Add(profit);
                }
            }
        }

        // 2. Read transactions from .hui file
        var database = new QuantitativeDatabase();
        var transactionsMap = new Dictionary<int, List<QItem>>();

        using (var reader = new StreamReader(huiPath))
        {
            var line = reader.ReadLine(); // First line: numTransactions numItems
            if (line == null)
            {
                return database;
            }

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    continue;
                }

                var tid = int.Parse(parts[0]);
                var itemId = int.Parse(parts[1]);
                var quantity = double.Parse(parts[2]);

                if (itemId == 0)
                {
                    continue; // Ignore item-id == 0
                }

                // Profit of item with ID = 1 is the first entry (index 0)
                double profit = 0;
                if (itemId >= 0 && itemId < profits.Count)
                {
                    profit = profits[itemId];
                }

                var utility = (long)Math.Round(quantity * profit);

                if (!transactionsMap.TryGetValue(tid, out var items))
                {
                    items = [];
                    transactionsMap[tid] = items;
                }
                items.Add(new QItem(itemId, utility));
            }
        }

        // Convert transactions map to database list
        foreach (var (tid, items) in transactionsMap)
        {
            var tu = items.Sum(item => item.Utility);

            database.Transactions.Add(new Transaction
            {
                Tid = tid,
                QItems = items,
                TU = tu
            });
        }
        
        Console.WriteLine("Finished reading .hui and .pro files. Total transactions: " + database.Transactions.Count);
        return database;
    }
}
