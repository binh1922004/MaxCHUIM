using System;
using System.Collections.Generic;
using System.Linq;
using MaxCHUIM.Algorithms;
using MaxCHUIM.Models;
using MaxCHUIM.Utilities;

namespace MaxCHUIM.Tests
{
    public class UtilityChecker
    {
        public static void CheckFakeHUIs()
        {
            const string huiFile = "/Users/mac/BINH/NCKH/Dataset/HUI/mushroom.hui";
            const string proFile = "/Users/mac/BINH/NCKH/Dataset/PRO/mushroom.pro";
            var db = HuiProReader.Read(huiFile, proFile);
            var mu = (long)(0.9 * db.Transactions.Count);

            var baseAlgo = new MaxCHuimAlgorithm();
            var bmAlgo = new BmMaxHuiAlgorithm();

            var baseRes = baseAlgo.Run(db, mu, AlgorithmMode.MaxCHUI);
            var bmRes = bmAlgo.Run(db, mu, AlgorithmMode.MaxCHUI);

            Console.WriteLine($"MaxCHuim MaxHUIs: {baseRes.MaxHUIs.Count}");
            Console.WriteLine($"BmMaxHui MaxHUIs: {bmRes.MaxHUIs.Count}");

            int fakeCountBase = 0;
            foreach (var mh in baseRes.MaxHUIs)
            {
                long trueUtil = CalculateExactUtility(db, mh.Itemset.Items);
                if (trueUtil < mu)
                {
                    fakeCountBase++;
                }
            }

            int fakeCountBm = 0;
            foreach (var mh in bmRes.MaxHUIs)
            {
                long trueUtil = CalculateExactUtility(db, mh.Itemset.Items);
                if (trueUtil < mu)
                {
                    fakeCountBm++;
                }
            }

            Console.WriteLine($"MaxCHuim Fake HUIs (Utility < {mu}): {fakeCountBase}");
            Console.WriteLine($"BmMaxHui Fake HUIs (Utility < {mu}): {fakeCountBm}");
        }

        private static long CalculateExactUtility(QuantitativeDatabase db, IReadOnlyList<int> items)
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
    }
}
