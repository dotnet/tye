# Schema

Configuring a schema for tye.yaml

1. Install the [Yaml](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) extension.
2. Open VS Code's settings (`CTRL+,`)
3. Add a mapping for our schema.

```js
{
  "yaml.schemas": {
    "https://raw.githubusercontent.com/dotnet/tye/main/src/schema/tye-schema.json": [
      "tye.yaml"
    ]
  }
}
```
