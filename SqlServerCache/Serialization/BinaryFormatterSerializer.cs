using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SqlServerCache.Serialization
{
    /// <summary>
    /// Serializes and deserializes objects using the BinaryFormatter.
    /// Note: Objects must be marked with the [Serializable] attribute.
    /// </summary>
    public class BinaryFormatterSerializer : ICacheSerializer
    {
        /// <summary>
        /// Serializes an object to a byte array using BinaryFormatter.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="value">The object to serialize.</param>
        /// <returns>A byte array containing the serialized object.</returns>
        public byte[] Serialize<T>(T value) where T : class
        {
            if (value == null)
                return null;

            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, value);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a byte array to an object using BinaryFormatter.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="data">The byte array to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        public T Deserialize<T>(byte[] data) where T : class
        {
            if (data == null || data.Length == 0)
                return default;

            using (var stream = new MemoryStream(data))
            {
                var formatter = new BinaryFormatter();
                return formatter.Deserialize(stream) as T;
            }
        }
    }
}
