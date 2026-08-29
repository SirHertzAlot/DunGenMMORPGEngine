using System.Security.Cryptography;

namespace Authoritative.Domain;

public class Item
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public Dictionary<string, string> Components { get; set; } = new();
}

public class ActionMessage
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, string>? Payload { get; set; }
}

public class ItemGenerator : IItemGenerator
{
    static readonly string[] ItemTypes = new[] { "sword", "shield", "potion", "bow", "staff", "armor" };
    static readonly string[] Tiers = new[] { "common", "uncommon", "rare", "epic", "legendary" };
    static readonly string[] Components = new[] { "damage", "durability", "enchantment", "weight", "material" };

    readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    public Item GenerateUniqueItem()
    {
        var type = ItemTypes[RandomIndex(ItemTypes.Length)];
        var tier = Tiers[RandomIndex(Tiers.Length)];
        var id = Guid.NewGuid().ToString();

        var item = new Item { Id = id, Type = type, Tier = tier };

        // Start with empty components and apply random constraints until "complete"
        var compCount = 1 + RandomIndex(Components.Length);
        var applied = new HashSet<string>();
        while (item.Components.Count < compCount)
        {
            var c = Components[RandomIndex(Components.Length)];
            if (applied.Add(c))
            {
                item.Components[c] = RandomComponentValue(c);
            }
        }

        return item;
    }

    int RandomIndex(int max)
    {
        var buf = new byte[4];
        _rng.GetBytes(buf);
        var v = BitConverter.ToUInt32(buf, 0);
        return (int)(v % (uint)max);
    }

    string RandomComponentValue(string key)
    {
        return key switch
        {
            "damage" => (10 + RandomIndex(90)).ToString(),
            "durability" => (50 + RandomIndex(50)).ToString(),
            "enchantment" => (RandomIndex(5) == 0 ? "fire" : "none"),
            "weight" => (1 + RandomIndex(20)).ToString(),
            "material" => (RandomIndex(2) == 0 ? "iron" : "steel"),
            _ => "0",
        };
    }
}
