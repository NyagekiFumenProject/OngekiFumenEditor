Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish -c Release -o "bin/publish" -m:8 OngekiFumenEditor.Avalonia.Browser.csproj

Push-Location "bin/publish/wwwroot"
try {
    dotnet serve -p 12999
}
finally {
    Pop-Location
}