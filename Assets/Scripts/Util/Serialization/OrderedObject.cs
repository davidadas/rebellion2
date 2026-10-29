namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Associates one existing object with its logical collection position.
    /// </summary>
    public readonly struct OrderedObject
    {
        public object Value { get; }
        public int Position { get; }

        /// <summary>
        /// Associates an existing graph object with its logical position.
        /// </summary>
        /// <param name="value">The existing object reference.</param>
        /// <param name="position">The logical position occupied by the object.</param>
        public OrderedObject(object value, int position)
        {
            Value = value;
            Position = position;
        }
    }
}
