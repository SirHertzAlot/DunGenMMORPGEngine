using Xunit;
using Authoritative.Domain;

namespace Authoritative.Tests;

public class ItemGeneratorTests
{
    [Fact]
    public void GenerateUniqueItem_ReturnsItemWithIdAndTypeAndComponents()
    {
        var gen = new ItemGenerator();
        var item = gen.GenerateUniqueItem();

        Assert.False(string.IsNullOrWhiteSpace(item.Id));
        Assert.False(string.IsNullOrWhiteSpace(item.Type));
        Assert.False(string.IsNullOrWhiteSpace(item.Tier));
        Assert.NotNull(item.Components);
        Assert.NotEmpty(item.Components);
    }
}
