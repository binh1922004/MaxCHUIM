using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MaxCHUIM.Models;

public class Itemset : IEquatable<Itemset>, IEnumerable<int>
{
    public int[] Items { get; }
    public int Count => Items.Length;
    private readonly int _hashCode;

    public Itemset(int[] items, bool needSort = true)
    {
        if (needSort)
        {
            var sorted = new int[items.Length];
            Array.Copy(items, sorted, items.Length);
            Array.Sort(sorted);
            Items = sorted;
        }
        else
        {
            Items = items;
        }

        // Precompute hash code
        var hash = Items.Aggregate(17, (current, item) => current * 31 + item);
        _hashCode = hash;
    }

    public Itemset(IEnumerable<int> items) : this(items.ToArray(), true)
    {
    }

    public bool IsSupersetOf(Itemset other)
    {
        if (other.Count > this.Count) return false;
        
        int i = 0, j = 0;
        while (i < this.Count && j < other.Count)
        {
            if (this.Items[i] == other.Items[j])
            {
                i++;
                j++;
            }
            else if (this.Items[i] < other.Items[j])
            {
                i++;
            }
            else
            {
                return false;
            }
        }
        return j == other.Count;
    }

    public bool IsSubsetOf(Itemset other)
    {
        return other.IsSupersetOf(this);
    }

    public bool Equals(Itemset? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_hashCode != other._hashCode) return false;
        if (Items.Length != other.Items.Length) return false;

        return !Items.Where((t, i) => t != other.Items[i]).Any();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Itemset);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public IEnumerator<int> GetEnumerator()
    {
        return ((IEnumerable<int>)Items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    public override string ToString()
    {
        return "{" + string.Join(", ", Items) + "}";
    }
}
