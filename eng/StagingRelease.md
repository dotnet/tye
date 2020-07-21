
## Releasing binary

1. Grab latest build off dnceng/internal.
2. Download it locally.
3. `dotnet nuget push <PACKAGE> --source https://api.nuget.org/v3/index.json -k <APIKEY>

## Updating repo to next version

- Update [getting started](/docs/getting_started.md) and other places in tutorial to just released version.
- Update [Working with CI builds](docs/getting_started.md) with next version.
- Update [Version.props](eng/Versions.props) to next version.