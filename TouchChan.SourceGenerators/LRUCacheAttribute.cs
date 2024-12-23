namespace TouchChan.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class LRUCacheAttribute(int maxSize = 128) : Attribute
    {
        public int MaxSize { get; } = maxSize;
    }
}
