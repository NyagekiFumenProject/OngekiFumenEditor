# ISA 逻辑回归

此控制台程序检查两点放置状态、有效组编号、删除组转移引用／删除引用及撤销、15% 组色与透明度。它不创建编辑器或窗口，不加入生产解决方案。

先构建 WPF 编辑器，再运行：

```powershell
dotnet run --project tests/isa-placement/IsaPlacement.csproj -c Release
```

如编辑器程序集输出位于其他目录，通过 `-p:EditorAssemblyDirectory="<程序集目录>"` 配置；默认使用仓库中编辑器的 Release 输出，不包含机器专用路径。
