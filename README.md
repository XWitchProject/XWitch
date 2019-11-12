XWitch
===

XWitch is a library for loading XML schemas. These files determine how to understand the type structure of an XML file. XWitch was made for Noita, but it can be used for other purposes as well.

Schema Format
===

To bridge the divide between Noita's C++ engine's schemas and external tools, you can use an XWitch Schema file. These are files written in a similar format to Noita's schemas that specify how types are serialized and deserialized to/from XML. There are four main type classes:

* Primitive - serializes a single attribute
* MultiAttr - serializes as multiple attributes with sub-fields separated by a dot
* Object - serializes as a child node in the XML tree
* List - serializes as a child node in the XML tree with a list of child nodes (list elements) inside it

Noita Schemas
===

Noita ships with schema files inside the data/schemas folder in the data.wak archive. To find out the latest hash, you can run `tools_modding/noita_dev.exe -build_schemas_n_exit` which will print it to stdout (and also create the file if it doesn't exist). These files determine how the game should parse XML files in order to load components (in the Entity Component System).

Unfortunately, this file is *only* for components and does not specify how the types used look like. This was the motivation to create this library in order to fill in the missing type information and allow using these files in external tools.

XWitch has the capability to read the type information from these files as long as it is provided with a file that will let it understand the C++ types used, e.g. `class ConfigGunActionInfo`. In order to accomplish this, another schema file has to be passed in that defines how the game serializes native types. You can see the de facto standard Noita "metaschema" over at the [Schemas](https://github.com/XWitchProject/Schemas/blob/master/ecs.xml) repository.

Once you have a meta-schema, you can use the `LoadFromNoitaSchema` method on the `Schema` object to load the files like a normal XWitch schema.

Documentation
===

[Click here to check out the complete API documentation.](https://xwitchproject.github.io/)

License
===

XWitch and [Schemas](https://github.com/XWitchProject/Schemas) are licensed under MIT.
