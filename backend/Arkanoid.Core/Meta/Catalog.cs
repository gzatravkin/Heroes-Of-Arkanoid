using System.Collections.Concurrent;
namespace Arkanoid.Core.Meta;

public abstract class Catalog<TDef> where TDef : class
{
    private readonly ConcurrentDictionary<string, TDef> _byId;

    protected Catalog(IEnumerable<TDef> defs, Func<TDef, string> key)
        => _byId = new ConcurrentDictionary<string, TDef>(defs.Select(d => KeyValuePair.Create(key(d), d)));

    public IEnumerable<TDef> All => _byId.Values;

    public TDef Get(string id) => _byId.TryGetValue(id, out var d) ? d
        : throw new KeyNotFoundException($"{typeof(TDef).Name} '{id}' not found.");

    public bool TryGet(string id, out TDef def)
    {
        if (_byId.TryGetValue(id, out var found)) { def = found; return true; }
        def = null!; return false;
    }

    protected void Upsert(string id, TDef def) => _byId[id] = def;
}
