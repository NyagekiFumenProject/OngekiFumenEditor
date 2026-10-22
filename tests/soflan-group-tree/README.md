# Soflan group tree model checks

Headless regression for the new `SoflanGroupTree` model and the `[SGC]`/`[SGF]` persistence format, including legacy `[SGWI]` compatibility. It does not create a window and is not part of `OngekiFumenEditor.sln`.

```bash
dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Release
dotnet run --project tests/soflan-group-tree/SoflanGroupTreeCheck.csproj -c Release -p:EditorAssemblyDirectory=<Release output directory>
```

The default `EditorAssemblyDirectory` is `OngekiFumenEditor/bin/Release/net10.0-windows`.
