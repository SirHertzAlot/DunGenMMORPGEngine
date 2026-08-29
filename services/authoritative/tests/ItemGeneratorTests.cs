using System;
using Authoritative.Domain;

#if UNITY_5_3_OR_NEWER
using Assert = NUnit.Framework.Assert;
using FactAttribute = NUnit.Framework.TestAttribute;
#else
using Assert = Xunit.Assert;
using FactAttribute = Xunit.FactAttribute;
#endif

namespace Authoritative.Tests
{
    public class ItemGeneratorTests
    {
        [FactAttribute]
        public void GenerateUniqueItem_ReturnsItemWithIdAndTypeAndComponents()
        {
            var gen = new ItemGenerator();
            var item = gen.GenerateUniqueItem();

            Assert.False(string.IsNullOrWhiteSpace(item.Id));
            Assert.False(string.IsNullOrWhiteSpace(item.Type));
            Assert.False(string.IsNullOrWhiteSpace(item.Tier));
            Assert.NotNull(item.Components);
#if UNITY_5_3_OR_NEWER
            Assert.IsNotEmpty(item.Components);
#else
            Assert.NotEmpty(item.Components);
#endif
        }
    }
}
