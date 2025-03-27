using System;

namespace SqlServerCache.Serialization
{
    /// <summary>
    /// Defines methods for serializing and deserializing cache items.
    /// </summary>
    public interface ICacheSerializer
    {
        /// <summary>
        /// Serializes an object to a byte array.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="value">The object to serialize.</param>
        /// <returns>A byte array containing the serialized object.</returns>
        byte[] Serialize<T>(T value) where T : class;

        /// <summary>
        /// Deserializes a byte array to an object.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="data">The byte array to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        T Deserialize<T>(byte[] data) where T : class;
    }
}
