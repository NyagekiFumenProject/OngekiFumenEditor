Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish -p:PublishProfile="win-x64-aot.pubxml" -o "bin/publish" -m:8 OngekiFumenEditor.Avalonia.Desktop.csproj