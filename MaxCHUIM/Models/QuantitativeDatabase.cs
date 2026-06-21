using System.Collections.Generic;

namespace MaxCHUIM.Models;

public class QuantitativeDatabase
{
    public List<Transaction> Transactions { get; set; } = new();
    public Dictionary<int, int> ProfitTable { get; set; } = new();
    public int NumberOfTransactions => Transactions.Count;
    public int NumberOfItems { get; set; }
}
