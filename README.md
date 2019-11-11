XWitch
===

Library for loading schemas that define how types should be parsed from XML. Made for Noita, but can be used for other purposes as well.

Noita Schemas
===

Noita ships with schema files inside the data/schemas folder in the data.wak archive. To find out the latest hash, you can run `tools_modding/noita_dev.exe -build_schemas_n_exit`, which will print it to stdout (and also create the file if it doesn't exist). These files determine how the game should parse XML files in order to load components (in the Entity Component System).

Unfortunately, this file is *only* for components and does not specify how the types used look like. This was the motivation to create this library in order to fill in the missing type information and allow using these files in external tools.

MetaSchema Format
===

To bridge the divide between the C++ engine's schemas and external tools, you can use a MetaSchema. These are files written in a similar format to Noita's schemas that specify how types are serialized and deserialized to/from XML. There are four main types:

* Primitive
* MultiAttr
* Object
* List

A `Primitive` type serializes into a single field, and maps into an internal type. Every other type eventually reduces to this. `MultiAttr`s are types that serialize into multiple attributes - for example Noita's Vector2, which serializes into attributes suffixed by `.x` and `.y`. `Object`s are types that serialize into their own child nodes (and have their own fields). Finally, `List` types serialize into their own child node that is simply a list of sub-child nodes which are the actual elements of the list.

TODO

License
===

XWitch and [Schemas](https://github.com/XWitchProject/Schemas) are licensed under MIT.
