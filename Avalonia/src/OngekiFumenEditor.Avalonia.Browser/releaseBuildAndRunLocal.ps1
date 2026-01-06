Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish -c Release -o "bin/publish" OngekiFumenEditor.Avalonia.Browser.csproj

Push-Location "bin/publish/wwwroot"
try {
    dotnet serve -h "Cross-Origin-Embedder-Policy:require-corp" -h "Cross-Origin-Opener-Policy:same-origin" -p 12999
}
finally {
    Pop-Location
}