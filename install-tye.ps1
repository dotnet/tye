
$versionPrefix = Select-Xml -Path .\eng\Versions.props -XPath "/Project/PropertyGroup/VersionPrefix" | ForEach-Object { $_.Node.InnerXml }

dotnet tool install microsoft.tye -g --version "$versionPrefix-dev" --add-source ./artifacts/packages/Debug/Shipping
