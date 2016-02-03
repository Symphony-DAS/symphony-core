namespace Symphony.Core
{
    /// <summary>
    /// Represents an external resource (image, data table, proprietary data file, etc).
    /// </summary>
    public class Resource
    {
        public Resource(string uti, string name, byte[] data)
        {
            UTI = uti;
            Name = name;
            Data = data;
        }

        /// <summary>
        /// The Uniform Type Identifier of this resource's data.
        /// </summary>
        public string UTI { get; private set; }

        /// <summary>
        /// The name of this Resource.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The raw data bytes of this Resource.
        /// </summary>
        public byte[] Data { get; private set; }
    }
}