using System.Collections.Generic;

// A simple item-count store used for both agent carry inventory
// and home pantries. Designed to expand as more ItemTypes are added.
public class Inventory
{
    private Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();

    public int Get(ItemType type)
    {
        return items.TryGetValue(type, out int count) ? count : 0;
    }

    public void Add(ItemType type, int count = 1)
    {
        items[type] = Get(type) + count;
    }

    public bool Remove(ItemType type, int count = 1)
    {
        if (!Has(type, count)) return false;
        items[type] = Get(type) - count;
        return true;
    }

    public bool Has(ItemType type, int count = 1)
    {
        return Get(type) >= count;
    }
}
