namespace ImageCompare;

/// <summary>
/// Burkhard-Keller tree over a discrete metric. Used for both the 64-bit dHash
/// (Hamming distance) and the 128-bit SimHash index.
/// </summary>
internal sealed class BkTree<T> where T : class
{
    private sealed class Node(T item)
    {
        public T Item { get; } = item;

        public Dictionary<int, Node> Children { get; } = new();
    }

    private readonly Func<T, T, int> _distance;
    private Node? _root;

    public BkTree(Func<T, T, int> distance)
    {
        _distance = distance;
    }

    public void Add(T item)
    {
        if (_root is null)
        {
            _root = new Node(item);
            return;
        }

        Node current = _root;

        while (true)
        {
            int distance = _distance(current.Item, item);

            if (current.Children.TryGetValue(distance, out Node? child))
            {
                current = child;
                continue;
            }

            current.Children[distance] = new Node(item);
            return;
        }
    }

    public T? FindNearest(T query, int maximumDistance, Func<T, bool>? predicate = null)
    {
        T? best = null;
        int bestDistance = int.MaxValue;

        foreach (T candidate in Search(query, maximumDistance))
        {
            int distance = _distance(query, candidate);

            if (distance < bestDistance && (predicate is null || predicate(candidate)))
            {
                best = candidate;
                bestDistance = distance;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return best;
    }

    public IEnumerable<T> Search(T query, int maximumDistance)
    {
        if (_root is null)
        {
            yield break;
        }

        Stack<Node> pending = new();
        pending.Push(_root);

        while (pending.Count > 0)
        {
            Node node = pending.Pop();
            int distance = _distance(query, node.Item);

            if (distance <= maximumDistance)
            {
                yield return node.Item;
            }

            int minimumEdge = distance - maximumDistance;
            int maximumEdge = distance + maximumDistance;

            foreach ((int edge, Node child) in node.Children)
            {
                if (edge >= minimumEdge && edge <= maximumEdge)
                {
                    pending.Push(child);
                }
            }
        }
    }
}
