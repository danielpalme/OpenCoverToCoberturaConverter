using System;
using System.Xml.Linq;

namespace Palmmedia.OpenCoverToCoberturaConverter
{
    public static class Extensions
    {
        /// <summary>
        /// Determines whether a <see cref="XElement"/> has an <see cref="XAttribute"/> with the given value..
        /// </summary>
        /// <param name="element">The <see cref="XElement"/>.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <param name="attributeValue">The attribute value.</param>
        /// <returns>
        ///   <c>true</c> if <see cref="XElement"/> has an <see cref="XAttribute"/> with the given value; otherwise, <c>false</c>.
        /// </returns>
        public static bool HasAttributeWithValue(this XElement element, XName attributeName, string attributeValue)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            XAttribute attribute = element.Attribute(attributeName);

            if (attribute == null)
            {
                return false;
            }
            else
            {
                return string.Equals(attribute.Value, attributeValue, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}