using System.Xml;

namespace XWitch {
    /// <summary>
    /// Extension class that provides a few typed GetAttribute variants for primitives.
    /// </summary>
    public static class XmlPrimitiveHelper {
        /// <returns>The bool value (1 is true, 0 is false).</returns>
        public static bool? GetAttributeBool(this XmlElement elem, string attr_name) {
            var value = elem.GetAttributeString(attr_name);

            if (value != null) {
                if (value == "1") return true;
                if (value == "0") return false;
            }

            return null;
        }

        /// <returns>The int value.</returns>
        public static int? GetAttributeInt(this XmlElement elem, string attr_name) {
            var value = elem.GetAttributeString(attr_name);

            if (value != null) {
                if (int.TryParse(value, out int p)) {
                    return p;
                }
            }

            return null;
        }

        /// <returns>The float value.</returns>
        public static float? GetAttributeFloat(this XmlElement elem, string attr_name) {
            var value = elem.GetAttributeString(attr_name);

            if (value != null) {
                if (float.TryParse(value, out float p)) {
                    return p;
                }
            }

            return null;
        }

        /// <returns>The double value.</returns>
        public static double? GetAttributeDouble(this XmlElement elem, string attr_name) {
            var value = elem.GetAttributeString(attr_name);

            if (value != null) {
                if (double.TryParse(value, out double p)) {
                    return p;
                }
            }

            return null;
        }

        /// <returns>The uint value.</returns>
        public static uint? GetAttributeUnsignedInt(this XmlElement elem, string attr_name) {
            var value = elem.GetAttributeString(attr_name);

            if (value != null) {
                if (uint.TryParse(value, out uint p)) {
                    return p;
                }
            }

            return null;
        }

        /// <summary>
        /// This method is very similar to GetAttribute, but it'll return null if the
        /// attribute doesn't exist.
        /// </summary>
        /// <returns>The string value.</returns>
        public static string GetAttributeString(this XmlElement elem, string attr_name) {
            if (!elem.HasAttribute(attr_name)) return null;
            return elem.GetAttribute(attr_name);
        }
    }
}