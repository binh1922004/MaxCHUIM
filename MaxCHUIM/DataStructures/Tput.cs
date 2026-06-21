using System.Collections.Generic;
using MaxCHUIM.Models;
using MaxCHUIM.Utilities;

namespace MaxCHUIM.DataStructures;

public class Tput
{
    public TputNode Root { get; } = new TputNode(-1, 0, null);
    public Dictionary<int, TputNode> HeaderTable { get; } = new();
    public List<TputNode> NodesById { get; } = new();
    private int _nextNid = 1;

    public Tput()
    {
        NodesById.Add(Root); // Root has Nid 0
    }

    public void Build(ReducedDatabase rd)
    {
        foreach (var tx in rd.Transactions)
        {
            var currentNode = Root;
            long prefixUtility = 0;

            foreach (var qi in tx.QItems)
            {
                if (!currentNode.Children.TryGetValue(qi.Item, out var childNode))
                {
                    childNode = new TputNode(qi.Item, _nextNid++, currentNode);
                    currentNode.Children[qi.Item] = childNode;
                    NodesById.Add(childNode);

                    // Link to header table (new nodes prepend to chain)
                    if (HeaderTable.TryGetValue(qi.Item, out var head))
                    {
                        childNode.NextLink = head;
                    }
                    HeaderTable[qi.Item] = childNode;
                }

                // Store both the individual item utility and the cumulative prefix utility
                childNode.Lu.Add((tx.Tid, qi.Utility, prefixUtility));
                childNode.ItemUtilitySum += qi.Utility;
                prefixUtility += qi.Utility;
                currentNode = childNode;
            }
        }

        // Sort every node's Lu by Tid ascending so two-pointer merges work correctly
        foreach (var node in NodesById)
        {
            if (node.Lu.Count > 1)
            {
                node.Lu.Sort((a, b) => a.Tid.CompareTo(b.Tid));
            }
        }
    }
}

