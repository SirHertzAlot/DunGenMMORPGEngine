#if !UNITY_5_3_OR_NEWER
namespace Authoritative.GraphQL
{
    /// <summary>
    /// Shared mutable counters used to prove DataLoader batching in tests: each
    /// DataLoader increments its counter once per LoadBatchAsync dispatch, so a
    /// parent query resolving a child field across N sessions produces exactly
    /// one batch dispatch per child-relationship rather than N+1.
    /// </summary>
    public sealed class DataLoaderCounters
    {
        public long RoomsBatches { get; set; }
        public long EnemiesBatches { get; set; }
        public long LootBatches { get; set; }
        public long EventsBatches { get; set; }
    }
}
#endif
