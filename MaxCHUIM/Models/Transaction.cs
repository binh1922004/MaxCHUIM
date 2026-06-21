using System.Collections.Generic;

namespace MaxCHUIM.Models;

public class Transaction
{
    public int Tid { get; set; }
    public List<QItem> QItems { get; set; } = new();
    public long TU { get; set; }

    public long GetItemUtility(int item)
    {
        var count = QItems.Count;
        for (var i = 0; i < count; i++)
        {
            if (QItems[i].Item == item)
            {
                return QItems[i].Utility;
            }
        }
        return 0;
    }
}
