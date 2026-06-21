using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaxCHUIM.Models;

namespace MaxCHUIM.Utilities;

public static class SpmfReader
{
    public static QuantitativeDatabase Read(string filePath)
    {
        var database = new QuantitativeDatabase();
        int tid = 0;

        using var reader = new StreamReader(filePath);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('@') || line.StartsWith('%'))
            {
                continue;
            }

            var parts = line.Split(':');
            if (parts.Length < 3)
            {
                // Fallback or ignore invalid lines
                continue;
            }

            string itemsPart = parts[0].Trim();
            string tuPart = parts[1].Trim();
            string utilsPart = parts[2].Trim();

            long tu = long.Parse(tuPart);

            var itemStrings = itemsPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var utilStrings = utilsPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (itemStrings.Length != utilStrings.Length)
            {
                throw new InvalidDataException($"Transaction at line has mismatch between items count ({itemStrings.Length}) and utilities count ({utilStrings.Length}).");
            }

            var transaction = new Transaction
            {
                Tid = tid++,
                TU = tu
            };

            for (int i = 0; i < itemStrings.Length; i++)
            {
                int item = int.Parse(itemStrings[i]);
                long utility = long.Parse(utilStrings[i]);
                transaction.QItems.Add(new QItem(item, utility));
            }

            database.Transactions.Add(transaction);
        }

        return database;
    }
}
