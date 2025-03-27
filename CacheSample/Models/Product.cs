using System;

namespace CacheSample.Models
{
    /// <summary>
    /// Represents a product for cache demonstration purposes.
    /// </summary>
    [Serializable]
    public class Product
    {
        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the product price.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the product description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the product category ID.
        /// </summary>
        public int CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Returns a string representation of the product.
        /// </summary>
        public override string ToString()
        {
            return $"Product {Id}: {Name} (${Price}) - {Description}";
        }
    }
}
