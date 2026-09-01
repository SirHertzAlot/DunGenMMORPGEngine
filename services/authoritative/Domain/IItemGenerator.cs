#if !UNITY_5_3_OR_NEWER
namespace Authoritative.Domain
{
    public interface IItemGenerator
    {
        Item GenerateUniqueItem();
    }
}
#endif
