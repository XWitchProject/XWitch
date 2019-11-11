using System.Xml;

namespace XWitch {
    public static class CengPrimitiveHelper {
        public static bool? GetAttributeBool(this XmlElement elem, string attr_name) {
            var value = elem.GetAttribute(attr_name);

            if (value != null) {
                if (value == "1") return true;
                if (value == "0") return false;
            }

            return null;
        }

        public static int? GetAttributeInt(this XmlElement elem, string attr_name) {
            var value = elem.GetAttribute(attr_name);

            if (value != null) {
                if (int.TryParse(value, out int p)) {
                    return p;
                }
            }

            return null;
        }

        public static float? GetAttributeFloat(this XmlElement elem, string attr_name) {
            var value = elem.GetAttribute(attr_name);

            if (value != null) {
                if (float.TryParse(value, out float p)) {
                    return p;
                }
            }

            return null;
        }

        public static double? GetAttributeDouble(this XmlElement elem, string attr_name) {
            var value = elem.GetAttribute(attr_name);

            if (value != null) {
                if (double.TryParse(value, out double p)) {
                    return p;
                }
            }

            return null;
        }

        public static uint? GetAttributeUnsignedInt(this XmlElement elem, string attr_name) {
            var value = elem.GetAttribute(attr_name);

            if (value != null) {
                if (uint.TryParse(value, out uint p)) {
                    return p;
                }
            }

            return null;
        }
    }
}