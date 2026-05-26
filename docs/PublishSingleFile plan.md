OngekiFumenEditor当前使用Costura.Fody实现单文件程序。即dotnet publish后，除了项目程序自己exe以及同名dll，没有其他多余的依赖dll文件存在。考虑使用MSBuild的PublishSingleFile替代Costura.Fody

目标
1. 使用Msbuild的PublishSingleFile替代Costura.Fody相关功能
2. dotnet publish .\OngekiFumenEditor\OngekiFumenEditor.csproj --no-restore -c RELEASE -o C:\ogkrEditorBuild\ --no-restore --disable-build-servers --force 
   编译完成后 ogkrEditorBuild除了OngekiFumenEditor.dll没有其他dll出现(OngekiFumenEditor.CommandLine.dll和runtimes目录和*.resources.dll除外)
3. shell执行命令 OngekiFumenEditor.CommandLine.exe convert --inputFile "F:\\OngekiFumenEditor\\OngekiFumenEditor.Benchmark\\Data\\FumenSamples\\20993_04.ogkr" --outputFile "F:\\ogkrEditorBuild\\test\\20993_04.nyageki" 检查20993_04.nyageki是否正确生成
