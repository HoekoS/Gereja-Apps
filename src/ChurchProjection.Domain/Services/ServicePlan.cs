using ChurchProjection.Domain.Library;

namespace ChurchProjection.Domain.Services;

public sealed class ServicePlan
{
    private readonly List<ServiceItem> _items = [];

    public required ServiceId Id { get; init; }

    public required string Name { get; set; }

    public required DateOnly ServiceDate { get; set; }

    /// <summary>Always in position order.</summary>
    public IReadOnlyList<ServiceItem> Items => _items;

    public void Load(IEnumerable<ServiceItem> items)
    {
        _items.Clear();
        _items.AddRange(items.OrderBy(item => item.Position));
        Renumber();
    }

    public ServiceItem Append(ServiceItem item)
    {
        _items.Add(item);
        Renumber();

        return item;
    }

    public bool Remove(string itemId)
    {
        var removed = _items.RemoveAll(item => item.Id == itemId) > 0;
        Renumber();

        return removed;
    }

    public ServiceItem? Find(string itemId) => _items.SingleOrDefault(item => item.Id == itemId);

    /// <summary>
    /// Reorders to exactly the given ids. Returns false and changes nothing
    /// unless the list is a permutation of the current items — a partial
    /// reorder would silently drop whatever the caller forgot.
    /// </summary>
    public bool Reorder(IReadOnlyList<string> itemIds)
    {
        if (itemIds.Count != _items.Count || itemIds.Distinct().Count() != itemIds.Count)
        {
            return false;
        }

        var byId = _items.ToDictionary(item => item.Id);

        if (!itemIds.All(id => byId.ContainsKey(id)))
        {
            return false;
        }

        var reordered = itemIds.Select(id => byId[id]).ToList();

        _items.Clear();
        _items.AddRange(reordered);
        Renumber();

        return true;
    }

    private void Renumber()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Position = i;
        }
    }
}
