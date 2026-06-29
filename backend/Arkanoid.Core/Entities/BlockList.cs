using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Arkanoid.Core.Entities;

/// <summary>
/// The game's mutable block collection. Every structural change (add / remove / replace /
/// clear) automatically runs the supplied callback, so the spatial index and the snapshot
/// version stay coherent — callers never have to remember to invalidate. This closes a whole
/// class of "added a block but forgot to bump the version" staleness bugs.
/// </summary>
public sealed class BlockList : Collection<Block>
{
    private readonly Action _onChange;

    public BlockList(Action onChange) => _onChange = onChange;

    protected override void InsertItem(int index, Block item) { base.InsertItem(index, item); _onChange(); }
    protected override void RemoveItem(int index)             { base.RemoveItem(index);        _onChange(); }
    protected override void SetItem(int index, Block item)    { base.SetItem(index, item);     _onChange(); }
    protected override void ClearItems()                      { base.ClearItems();             _onChange(); }

    /// <summary>Append many blocks, invalidating once.</summary>
    public void AddRange(IEnumerable<Block> items)
    {
        bool any = false;
        foreach (var b in items) { base.InsertItem(Count, b); any = true; }
        if (any) _onChange();
    }

    /// <summary>Remove all matching blocks, invalidating once if anything was removed. Returns the count.</summary>
    public int RemoveAll(Predicate<Block> match)
    {
        int removed = 0;
        for (int i = Count - 1; i >= 0; i--)
            if (match(this[i])) { base.RemoveItem(i); removed++; }
        if (removed > 0) _onChange();
        return removed;
    }

    /// <summary>First matching block, or null (List&lt;T&gt;.Find parity for read-only callers/tests).</summary>
    public Block? Find(Predicate<Block> match)
    {
        foreach (var b in this) if (match(b)) return b;
        return null;
    }

    /// <summary>All matching blocks (List&lt;T&gt;.FindAll parity for read-only callers/tests).</summary>
    public List<Block> FindAll(Predicate<Block> match)
    {
        var result = new List<Block>();
        foreach (var b in this) if (match(b)) result.Add(b);
        return result;
    }

    /// <summary>List&lt;T&gt;.TrueForAll parity.</summary>
    public bool TrueForAll(Predicate<Block> match)
    {
        foreach (var b in this) if (!match(b)) return false;
        return true;
    }

    /// <summary>List&lt;T&gt;.Exists parity.</summary>
    public bool Exists(Predicate<Block> match)
    {
        foreach (var b in this) if (match(b)) return true;
        return false;
    }
}
