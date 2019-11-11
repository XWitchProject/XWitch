using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HtmlAgilityPack;

namespace XWitch {
    public class NoitaSchemaVar {
        public string Name;
        public int Size;
        public MetaSchemaType Type;
    }

    public class NoitaSchemaComponent {
        public string ComponentName;
        public List<NoitaSchemaVar> Vars = new List<NoitaSchemaVar>();
    }

    public class NoitaSchema {
        public List<NoitaSchemaComponent> Components = new List<NoitaSchemaComponent>();
        public Dictionary<string, NoitaSchemaComponent> ComponentsByKey = new Dictionary<string, NoitaSchemaComponent>();

        public NoitaSchemaComponent TryGetComponent(string name) {
            if (ComponentsByKey.TryGetValue(name, out NoitaSchemaComponent comp)) {
                return comp;
            }
            return null;
        }

        public void Load(MetaSchema metaschema, StreamReader reader) {
            Components = new List<NoitaSchemaComponent>();
            ComponentsByKey = new Dictionary<string, NoitaSchemaComponent>();
            var doc = new HtmlDocument();

            doc.Load(reader);

            if (doc.DocumentNode.ChildNodes.Count == 0) return;

            var root = doc.DocumentNode.ChildNodes[0];
            if (root.NodeType != HtmlNodeType.Element || root.Name != "Schema") {
                return;
            }

            for (var i = 0; i < root.ChildNodes.Count; i++) {
                var child = root.ChildNodes[i];
                if (child.NodeType != HtmlNodeType.Element) continue;
                var comp = new NoitaSchemaComponent();

                Components.Add(comp);

                comp.ComponentName = child.GetAttributeValue("component_name", null);

                ComponentsByKey[comp.ComponentName] = comp;

                for (var j = 0; j < child.ChildNodes.Count; j++) {
                    var varchild = child.ChildNodes[j];
                    if (varchild.NodeType != HtmlNodeType.Element) continue;
                    var cvar = new NoitaSchemaVar();

                    cvar.Name = varchild.GetAttributeValue("name", null);
                    cvar.Size = int.Parse(varchild.GetAttributeValue("size", null));

                    var override_type = metaschema.GetSchemaOverride(comp.ComponentName, cvar.Name);

                    if (override_type == null) {
                        var ceng_type_name = varchild.GetAttributeValue("type", null).Trim();

                        cvar.Type = metaschema.GetMetaSchemaType(ceng_type_name);

                        if (cvar.Type.ExpectedSize != cvar.Size) {
                            throw new Exception($"Type size mismatch: metaschema-defined type '{cvar.Type.Name}' expects size to be {cvar.Type.ExpectedSize}, but actual size of field '{cvar.Name}' in component '{comp.ComponentName}' is {cvar.Size}");
                        }
                    } else {
                        cvar.Type = override_type;
                    }

                    comp.Vars.Add(cvar);
                }
            }
        }
    }
}