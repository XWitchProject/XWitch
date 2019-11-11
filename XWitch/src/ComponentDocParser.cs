using System;
using System.IO;
using System.Collections.Generic;

public class ComponentDoc {
    public Dictionary<string, Dictionary<string, string>> DocMap = new Dictionary<string, Dictionary<string, string>>();

    public string GetDocumentation(string component, string field) {
        Dictionary<string, string> component_docs = null;
        if (!DocMap.TryGetValue(component, out component_docs)) return null;

        string doc = null;
        component_docs.TryGetValue(field, out doc);

        return doc;
    }

    public void Load(StreamReader reader) {
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
}