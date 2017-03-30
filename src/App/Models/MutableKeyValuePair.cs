namespace LostTech.Stack.Models
{
    public sealed class MutableKeyValuePair<TKey, TValue>
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }


        public MutableKeyValuePair() { }
        public MutableKeyValuePair(TKey key, TValue value)
        {
            this.Key = key;
            this.Value = value;
        }
    }
}
