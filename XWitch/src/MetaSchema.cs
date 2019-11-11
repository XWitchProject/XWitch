using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HtmlAgilityPack;

namespace XWitch {
    public abstract class MetaSchemaType {
        public string Name;
        public int ExpectedSize;
        public bool Unknown;
        private string _NiceName;
        public string NiceName {
            get { return _NiceName ?? Name; }
            set { _NiceName = value; }
        }
    }

    public class MetaSchemaPrimitiveType : MetaSchemaType {
        public Type Type;
    }

    public class MetaSchemaObjectType : MetaSchemaType {
        public bool ArrayType;
        public List<string> FieldNames;
        public Dictionary<string, MetaSchemaType> FieldTypes;
    }

    public class MetaSchemaMultiAttrType : MetaSchemaType {
        public List<string> SubAttributeNames;
        public Dictionary<string, MetaSchemaType> SubAttributeTypes;
    }

    public class MetaSchemaListType : MetaSchemaType {
        public bool ArrayType;
        public string EntryName;
        public List<string> AttributeNames;
        public Dictionary<string, MetaSchemaType> AttributeTypes;
    }

    public class MetaSchema {
        public string ID;
        public string ParentID;
        public MetaSchema Parent;
        public List<string> MetaSchemaTypes;
        public Dictionary<string, MetaSchemaType> MetaSchemaTypeMap;
        public Dictionary<string, Dictionary<string, string>> SchemaOverrides;

        public static Dictionary<string, MetaSchema> Library = new Dictionary<string, MetaSchema>();

        public static void AddToLibrary(MetaSchema msch) {
            Library.Add(msch.ID, msch);
        }

        public static readonly Dictionary<string, Type> NETTypeMap = new Dictionary<string, Type> {
            ["int"] = typeof(int),
            ["uint"] = typeof(uint),
            ["bool"] = typeof(bool),
            ["float"] = typeof(float),
            ["double"] = typeof(double),
            ["string"] = typeof(string),
            ["unsigned char"] = typeof(byte)
        };

        public MetaSchemaType GetSchemaOverride(string component_name, string var_name) {
            if (SchemaOverrides.TryGetValue(component_name, out Dictionary<string, string> comp_override)) {
                if (comp_override.TryGetValue(var_name, out string type_override)) {
                    return GetMetaSchemaType(type_override);
                }
            }

            return Parent?.GetSchemaOverride(component_name, var_name);
        }

        public void Load(StreamReader reader, MetaSchema parent = null) {
            MetaSchemaTypeMap = new Dictionary<string, MetaSchemaType>();
            MetaSchemaTypes = new List<string>();
            SchemaOverrides = new Dictionary<string, Dictionary<string, string>>();

            var doc = new XmlDocument();

            using (var xr = XmlReader.Create(reader)) {
                doc.Load(xr);
            }

            if (!doc.HasChildNodes) return;

            var root = doc.ChildNodes[0] as XmlElement;

            ID = root.GetAttribute("id");
            if (root.HasAttribute("inherit")) {
                ParentID = root.GetAttribute("inherit");

                if (parent == null) {
                    Library.TryGetValue(ParentID, out parent);
                }

                if (parent == null || parent.ID != ParentID) {
                    throw new Exception($"Missing (or mismatched) parent for MetaSchema: {parent?.ID ?? "<null>"}");
                }

                Parent = parent;
            }

            var unsized = root.GetAttributeBool("unsized");

            for (var i = 0; i < root.ChildNodes.Count; i++) {
                var child = root.ChildNodes[i] as XmlElement;
                if (child == null) continue;

                if (child.Name == "SchemaOverrides") {
                    for (var j = 0; j < child.ChildNodes.Count; j++) {
                        var ovr_child = child.ChildNodes[j] as XmlElement;
                        if (ovr_child == null) continue;

                        var component_name = ovr_child.GetAttribute("component");

                        Dictionary<string, string> comp_override;
                        if (!SchemaOverrides.TryGetValue(component_name, out comp_override)) {
                            comp_override = SchemaOverrides[component_name] = new Dictionary<string, string>();
                        }

                        comp_override[ovr_child.GetAttribute("var")] = ovr_child.GetAttribute("override_type");
                    }

                    continue;
                }

                var name = child.GetAttribute("name").Replace("[", "<").Replace("]", ">");

                if (child.Name == "Alias") {
                    var aliased_type = LoadAliasDef(child);
                    MetaSchemaTypeMap[name] = aliased_type;
                    continue;
                }

                var type = LoadTypeDef(child);

                if (unsized != true) {
                    if (!child.HasAttribute("size")) {
                        throw new Exception($"Missing 'size' attribute on metaschema type '{name}'");
                    }
                    var size_str = child.GetAttribute("size");
                    var size = int.Parse(size_str);
                    type.ExpectedSize = size;
                }

                type.Name = name;
                if (child.HasAttribute("nice_name")) {
                    type.NiceName = child.GetAttribute("nice_name");
                }

                if (child.Name == "FieldArray") {
                    if (!(type is MetaSchemaObjectType) && !(type is MetaSchemaListType)) {
                        throw new Exception($"FieldArray can only be used with Object and List types");
                    }
                    if (type is MetaSchemaListType) ((MetaSchemaListType)type).ArrayType = true;
                    else ((MetaSchemaObjectType)type).ArrayType = true;
                }

                MetaSchemaTypeMap[name] = type;
            }
        }

        private MetaSchemaType LoadTypeDef(XmlElement typedef) {
            switch (typedef.Name) {
            case "Primitive": return LoadPrimitiveDef(typedef);
            case "MultiAttr": return LoadMultiAttrDef(typedef);
            case "Object": return LoadObjectDef(typedef);
            case "List": return LoadListDef(typedef);
            default: throw new Exception($"Unknown typedef: '{typedef.Name}'");
            }
        }

        public MetaSchemaType TryGetMetaSchemaType(string name) {
            name = name.Replace("[", "<").Replace("]", ">");
            MetaSchemaType result = null;
            if (!MetaSchemaTypeMap.TryGetValue(name, out result)) {
                if (Parent != null) {
                    return Parent.GetMetaSchemaType(name);
                }
            }
            return result;
        }

        public MetaSchemaType GetMetaSchemaType(string name) {
            var mtype = TryGetMetaSchemaType(name);
            if (mtype == null) throw new Exception($"Tried to get predef type: '{name}', but it hasn't been registered yet!");
            return mtype;
        }

        private MetaSchemaPrimitiveType LoadPrimitiveDef(XmlElement primitive) {
            var t = new MetaSchemaPrimitiveType();
            t.Type = NETTypeMap[primitive.GetAttribute("type")];
            return t;
        }

        private MetaSchemaMultiAttrType LoadMultiAttrDef(XmlElement typedef) {
            var t = new MetaSchemaMultiAttrType();
            t.SubAttributeNames = new List<string>();
            t.SubAttributeTypes = new Dictionary<string, MetaSchemaType>();

            for (var i = 0; i < typedef.ChildNodes.Count; i++) {
                var field = typedef.ChildNodes[i] as XmlElement;
                if (field == null) continue;

                var name = field.GetAttribute("name");
                t.SubAttributeNames.Add(name);
                t.SubAttributeTypes[name] = GetMetaSchemaType(field.GetAttribute("predef"));
            }

            return t;
        }

        private MetaSchemaObjectType LoadObjectDef(XmlElement typedef) {
            var t = new MetaSchemaObjectType();
            t.FieldNames = new List<string>();
            t.FieldTypes = new Dictionary<string, MetaSchemaType>();

            for (var i = 0; i < typedef.ChildNodes.Count; i++) {
                var field = typedef.ChildNodes[i] as XmlElement;
                if (field == null) continue;

                var name = field.GetAttribute("name");
                t.FieldNames.Add(name);
                t.FieldTypes[name] = GetMetaSchemaType(field.GetAttribute("predef"));
            }

            return t;
        }

        private MetaSchemaListType LoadListDef(XmlElement typedef) {
            var t = new MetaSchemaListType();
            t.EntryName = typedef.GetAttribute("entry_name");
            t.AttributeNames = new List<string>();
            t.AttributeTypes = new Dictionary<string, MetaSchemaType>();

            for (var i = 0; i < typedef.ChildNodes.Count; i++) {
                var field = typedef.ChildNodes[i] as XmlElement;
                if (field == null) continue;

                var name = field.GetAttribute("name");
                t.AttributeNames.Add(name);
                t.AttributeTypes[name] = GetMetaSchemaType(field.GetAttribute("predef"));
            }

            return t;
        }

        private MetaSchemaType LoadAliasDef(XmlElement typedef) {
            return GetMetaSchemaType(typedef.GetAttribute("alias"));
        }
    }
}