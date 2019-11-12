using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HtmlAgilityPack;

namespace XWitch {
    /// <summary>
    /// Base class with all common fields for XWitch schema type categories.
    /// </summary>
    public abstract class SchemaType {
        public string Name;
        public int ExpectedSize;
        public bool Unknown;
        private string _NiceName;
        public string NiceName {
            get { return _NiceName ?? Name; }
            set { _NiceName = value; }
        }
    }

    /// <summary>
    /// Primitive type category.
    /// Serializes to single attribute on the object's XML node.
    /// </summary>
    public class SchemaPrimitiveType : SchemaType {
        public Type Type;
    }

    /// <summary>
    /// Object type category.
    /// Serializes to a child node on the object's XML node. May include its own fields of any type.
    /// </summary>
    public class SchemaObjectType : SchemaType {
        /// <summary>
        /// If true, this is a FieldArray (multiple objects of the same name may appear,
        /// and they are all part of a single list)
        /// </summary>
        public bool ArrayType;
        public List<string> FieldNames;
        public Dictionary<string, SchemaType> FieldTypes;
    }

    /// <summary>
    /// MultiAttr type category.
    /// Serializes to multiple attributes on the object's XML node, with `.` used as the field separator. May only have Primitive fields.
    /// </summary>
    public class SchemaMultiAttrType : SchemaType {
        public List<string> SubAttributeNames;
        public Dictionary<string, SchemaType> SubAttributeTypes;
    }

    /// <summary>
    /// List type category.
    /// Serializes to a basic child node on the object's XML node, with each list element
    /// represented as a sub-child node that may include fields of any type.
    /// </summary>
    public class SchemaListType : SchemaType {
        public bool ArrayType;
        public string EntryName;
        public List<string> AttributeNames;
        public Dictionary<string, SchemaType> AttributeTypes;
    }

    /// <summary>
    /// XWitch Schema
    /// </summary>
    public class Schema {
        /// <summary>
        /// Default ID used when loading Noita schemas (LoadFromNoitaSchema)
        /// </summary>
        public const string DEFAULT_NOITA_METASCHEMA_ID = "ecs";

        /// <summary>
        /// Unique identifier of this schema, used for the `inherit` attribute.
        /// </summary>
        public string ID;

        /// <summary>
        /// Identifier of the schema that this schema `inherit`s from, or null
        /// if it doesn't inherit from anything.
        /// </summary>
        public string ParentID;

        /// <summary>
        /// Schema that this schema `inherit`s from, or null if it doesn't inherit
        /// from anything.
        /// </summary>
        public Schema Parent;

        /// <summary>
        /// List of type names in this schema.
        /// </summary>
        public List<string> SchemaTypes;

        /// <summary>
        /// Map of type name to type for all types in this schema.
        /// </summary>
        public Dictionary<string, SchemaType> SchemaTypeMap;

        /// <summary>
        /// Map of Noita Schema overrides. Only used in the metaschema provided to
        /// LoadFromNoitaSchema.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> SchemaOverrides;

        /// <summary>
        /// Documentation for fields of types in this schema.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> DocMap = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// ID-indexed library of loaded schemas.
        /// </summary>
        public static Dictionary<string, Schema> Library = new Dictionary<string, Schema>();

        /// <summary>
        /// Adds a Schema object to the static library.
        /// </summary>
        /// <param name="msch">Schema to add.</param>
        public static void AddToLibrary(Schema msch) {
            Library.Add(msch.ID, msch);
        }

        /// <summary>
        /// Returns a Schema object with the associated ID, or null if one
        /// is not in the library.
        /// </summary>
        /// <returns>Schema with the specified ID.</returns>
        /// <param name="id">ID of the schema.</param>
        public static Schema TryGetSchema(string id) {
            Schema schema = null;
            Library.TryGetValue(id, out schema);
            return schema;
        }

        /// <summary>
        /// Maps type names for use in `Primitive`-class types to actual .NET types.
        /// </summary>
        public static readonly Dictionary<string, Type> NETTypeMap = new Dictionary<string, Type> {
            ["int"] = typeof(int),
            ["uint"] = typeof(uint),
            ["bool"] = typeof(bool),
            ["float"] = typeof(float),
            ["double"] = typeof(double),
            ["string"] = typeof(string),
            ["unsigned char"] = typeof(byte)
        };

        /// <summary>
        /// Returns the SchemaType that overrides the specified field, or null
        /// if no override for that field has been defined.
        /// </summary>
        /// <returns>XWitch schema type.</returns>
        /// <param name="type_name">Name of the schema type.</param>
        /// <param name="var_name">Name of the schema field.</param>
        public SchemaType GetSchemaOverride(string type_name, string var_name) {
            if (SchemaOverrides.TryGetValue(type_name, out Dictionary<string, string> comp_override)) {
                if (comp_override.TryGetValue(var_name, out string type_override)) {
                    return GetSchemaType(type_override);
                }
            }

            return Parent?.GetSchemaOverride(type_name, var_name);
        }

        /// <summary>
        /// Loads an XWitch schema file.
        /// </summary>
        /// <param name="reader">Stream reader.</param>
        /// <param name="parent">Optional parent (`inherit`) override.</param>
        public void Load(StreamReader reader, Schema parent = null) {
            ResetData();

            var doc = new XmlDocument();

            using (var xr = XmlReader.Create(reader)) {
                doc.Load(xr);
            }

            if (!doc.HasChildNodes) return;

            var root = doc.ChildNodes[0] as XmlElement;

            ID = root.GetAttribute("id");
            if (parent == null && root.HasAttribute("inherit")) {
                ParentID = root.GetAttribute("inherit");

                parent = null;
                Library.TryGetValue(ParentID, out parent);

                if (parent == null || parent.ID != ParentID) {
                    throw new Exception($"Missing (or mismatched) parent for Schema: {parent?.ID ?? "<null>"}");
                }

                Parent = parent;
            } else if (parent != null) {
                ParentID = parent.ID;
                Parent = parent;
            }

            var sized = root.GetAttributeBool("sized");

            if (TryGetSchema(ID) != null) {
                throw new Exception($"Schema with ID {ID} is already loaded");
            }

            AddToLibrary(this);

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
                    SchemaTypeMap[name] = aliased_type;
                    continue;
                }

                var type = LoadTypeDef(child);

                if (sized == true) {
                    if (!child.HasAttribute("size")) {
                        throw new Exception($"Missing 'size' attribute on Schema type '{name}'");
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
                    if (!(type is SchemaObjectType) && !(type is SchemaListType)) {
                        throw new Exception($"FieldArray can only be used with Object and List types");
                    }
                    if (type is SchemaListType) ((SchemaListType)type).ArrayType = true;
                    else ((SchemaObjectType)type).ArrayType = true;
                }

                AddSchemaType(type);
            }
        }

        /// <summary>
        /// Returns the schema type specified by the name, or null if one
        /// doesn't exist.
        /// </summary>
        /// <returns>The XWitch schema type.</returns>
        /// <param name="name">Name of the schema type.</param>
        public SchemaType TryGetSchemaType(string name) {
            name = name.Replace("[", "<").Replace("]", ">");
            SchemaType result = null;
            if (!SchemaTypeMap.TryGetValue(name, out result)) {
                if (Parent != null) {
                    return Parent.GetSchemaType(name);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the schema type specified by the name, or throws an exception
        /// if one doesn't exist.
        /// </summary>
        /// <returns>The XWitch schema type.</returns>
        /// <param name="name">Name of the schema type.</param>
        public SchemaType GetSchemaType(string name) {
            var mtype = TryGetSchemaType(name);
            if (mtype == null) throw new Exception($"Tried to get predef type: '{name}', but it hasn't been registered yet!");
            return mtype;
        }

        /// <summary>
        /// Loads a Noita schema file (data/schemas) as an XWitch schema object.
        /// </summary>
        /// <param name="reader">Stream reader.</param>
        /// <param name="id">Optional ID to use for this schema (`"noita"` by default).</param>
        /// <param name="metaschema">Optional XWitch schema object that defines unknown types used in the schema (will try to get an already registered schema with the ID `ecs` if this isn't passed).</param>
        public void LoadFromNoitaSchema(StreamReader reader, string id = null, Schema metaschema = null) {
            if (metaschema == null) {
                metaschema = Schema.TryGetSchema(DEFAULT_NOITA_METASCHEMA_ID);
                if (metaschema == null) {
                    throw new Exception($"No metaschema given to LoadNoitaSchema and no schema with the ID 'ecs' registered");
                }
            }

            if (id == null) id = "noita";
            ID = id;

            ResetData();
            var doc = new HtmlDocument();

            doc.Load(reader);

            if (doc.DocumentNode.ChildNodes.Count == 0) return;

            var root = doc.DocumentNode.ChildNodes[0];
            if (root.NodeType != HtmlNodeType.Element || root.Name != "Schema") {
                return;
            }

            if (TryGetSchema(ID) != null) {
                throw new Exception($"Schema with ID {ID} is already loaded");
            }

            AddToLibrary(this);

            for (var i = 0; i < root.ChildNodes.Count; i++) {
                var child = root.ChildNodes[i];
                if (child.NodeType != HtmlNodeType.Element) continue;
                var type = new SchemaObjectType();

                type.ArrayType = false;
                type.FieldNames = new List<string>();
                type.FieldTypes = new Dictionary<string, SchemaType>();
                type.Name = child.GetAttributeValue("component_name", null);

                AddSchemaType(type);

                for (var j = 0; j < child.ChildNodes.Count; j++) {
                    var varchild = child.ChildNodes[j];
                    if (varchild.NodeType != HtmlNodeType.Element) continue;

                    var name = varchild.GetAttributeValue("name", null);
                    var size = int.Parse(varchild.GetAttributeValue("size", null));

                    SchemaType field_type = null;
                    var override_type = metaschema.GetSchemaOverride(type.Name, name);

                    if (override_type == null) {
                        var ceng_type_name = varchild.GetAttributeValue("type", null).Trim();
                        field_type = metaschema.GetSchemaType(ceng_type_name);

                        if (field_type.ExpectedSize != size) {
                            throw new Exception($"Type size mismatch: metaschema-defined type '{field_type.Name}' expects size to be {field_type.ExpectedSize}, but actual size of field '{name}' in component '{type.Name}' is {size}");
                        }
                    } else {
                        field_type = override_type;
                    }

                    type.FieldNames.Add(name);
                    type.FieldTypes[name] = field_type;
                }
            }
        }

        /// <summary>
        /// Loads documentation for the XWitch schema in component_documentation.txt format.
        /// Please note that this does not reset existing documentation, so you can call this method
        /// more than once.
        /// </summary>
        /// <param name="reader">Stream reader.</param>
        public void LoadDocumentation(StreamReader reader) {
            Dictionary<string, string> current_component_dict = null;
            var in_members = false;

            while (!reader.EndOfStream) {
                var line = reader.ReadLine();

                if (line.Length == 0 || line.Trim().Length == 0) {
                    in_members = false;
                    continue;
                }

                // Component names are the only things that aren't indented
                if (line[0] != ' ') {
                    in_members = false;
                    if (!DocMap.TryGetValue(line, out current_component_dict)) {
                        current_component_dict = DocMap[line] = new Dictionary<string, string>();
                    }
                    continue;
                }

                var trimmed_line = line.Trim();

                // Start of field section header
                if (trimmed_line[0] == '-') {
                    // Name starts after a space
                    // then there's another space and the rest of the dashes
                    var section_header = trimmed_line.Substring(2, trimmed_line.LastIndexOf(' ') - 2);
                    if (section_header == "Members") {
                        in_members = true;
                    } else in_members = false;

                    continue;
                }

                if (!in_members) continue;

                // Only member lines will get here
                var member_name = trimmed_line.Substring(0, trimmed_line.IndexOf(' '));
                var first_quote_idx = trimmed_line.IndexOf('"');
                var member_desc = trimmed_line.Substring(first_quote_idx + 1, trimmed_line.LastIndexOf('"') - first_quote_idx - 1);

                if (member_desc.Trim().Length == 0) continue;

                if (current_component_dict == null) throw new Exception("Member line appeared before first component header");
                current_component_dict[member_name] = member_desc;
            }
        }

        /// <summary>
        /// Acquires registered type documentation.
        /// </summary>
        /// <returns>The documentation string or `null` if not defined.</returns>
        /// <param name="type">Name of the schema type.</param>
        /// <param name="field">Name of the schema type's field.</param>
        public string TryGetDocumentation(string type, string field) {
            Dictionary<string, string> type_docs = null;
            if (!DocMap.TryGetValue(type, out type_docs)) return null;

            string doc = null;
            type_docs.TryGetValue(field, out doc);

            return doc;
        }

        private void ResetData() {
            SchemaTypeMap = new Dictionary<string, SchemaType>();
            SchemaTypes = new List<string>();
            SchemaOverrides = new Dictionary<string, Dictionary<string, string>>();
        }

        private void AddSchemaType(SchemaType type) {
            SchemaTypeMap[type.Name] = type;
            SchemaTypes.Add(type.Name);
        }

        private SchemaType LoadTypeDef(XmlElement typedef) {
            switch (typedef.Name) {
            case "Primitive": return LoadPrimitiveDef(typedef);
            case "MultiAttr": return LoadMultiAttrDef(typedef);
            case "Object": return LoadObjectDef(typedef);
            case "List": return LoadListDef(typedef);
            default: throw new Exception($"Unknown typedef: '{typedef.Name}'");
            }
        }

        private SchemaPrimitiveType LoadPrimitiveDef(XmlElement primitive) {
            var t = new SchemaPrimitiveType();
            t.Type = NETTypeMap[primitive.GetAttribute("type")];
            return t;
        }

        private SchemaMultiAttrType LoadMultiAttrDef(XmlElement typedef) {
            var t = new SchemaMultiAttrType();
            t.SubAttributeNames = new List<string>();
            t.SubAttributeTypes = new Dictionary<string, SchemaType>();

            for (var i = 0; i < typedef.ChildNodes.Count; i++) {
                var field = typedef.ChildNodes[i] as XmlElement;
                if (field == null) continue;

                var name = field.GetAttribute("name");
                t.SubAttributeNames.Add(name);
                t.SubAttributeTypes[name] = GetSchemaType(field.GetAttribute("predef"));
            }

            return t;
        }

        private SchemaObjectType LoadObjectDef(XmlElement typedef) {
            var t = new SchemaObjectType();
            t.FieldNames = new List<string>();
            t.FieldTypes = new Dictionary<string, SchemaType>();

            for (var i = 0; i < typedef.ChildNodes.Count; i++) {
                var field = typedef.ChildNodes[i] as XmlElement;
                if (field == null) continue;

                var name = field.GetAttribute("name");
                t.FieldNames.Add(name);
                t.FieldTypes[name] = GetSchemaType(field.GetAttribute("predef"));
            }

            return t;
        }

        private SchemaListType LoadListDef(XmlElement typedef) {
            var t = new SchemaListType();
            t.EntryName = typedef.GetAttribute("entry_name");
            t.AttributeNames = new List<string>();
            t.AttributeTypes = new Dictionary<string, SchemaType>();

            for (var i = 0; i < typedef.ChildNodes.Count; i++) {
                var field = typedef.ChildNodes[i] as XmlElement;
                if (field == null) continue;

                var name = field.GetAttribute("name");
                t.AttributeNames.Add(name);
                t.AttributeTypes[name] = GetSchemaType(field.GetAttribute("predef"));
            }

            return t;
        }

        private SchemaType LoadAliasDef(XmlElement typedef) {
            return GetSchemaType(typedef.GetAttribute("alias"));
        }
    }
}