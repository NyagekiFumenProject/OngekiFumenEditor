using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel.DefaultImpl
{
	public class DefaultDocumentContext : IDocumentContext
	{
		public AdhocWorkspace WorkSpace { get; set; }
		public Document Document { get; set; }
		public CompletionService CompletionService { get; set; }

		public async IAsyncEnumerable<ICompletionItem> CompleteCode(string str, int cursorPos, bool insertOrDelete)
		{
			var document = Document.WithText(SourceText.From(str));
			var result = await CompletionService.GetCompletionsAsync(document, cursorPos);
			if (result is null)
				yield break;
			foreach (var item in result.ItemsList)
			{
				var desc = await CompletionService.GetDescriptionAsync(document, item);
				yield return new DefaultCompletionItem()
				{
					Description = desc.Text,
					Name = item.FilterText,
					Priority = 0
				};
			}
		}

		public BuildParam CreateBuildParam()
		{
			return new BuildParam();
		}

		public bool GenerateProjectFile(string genProjOutputDirPath, string scriptFilePath, out string projFilePath)
		{
			projFilePath = null;

			try
			{
				var root = ProjectRootElement.Create();
				root.Sdk = "Microsoft.NET.Sdk";

				var projCommonGroup = root.AddPropertyGroup();
				projCommonGroup.AddProperty("TargetFramework", "net10.0-windows");
				projCommonGroup.AddProperty("OutputType", "Library");
				projCommonGroup.AddProperty("UseWPF", "true");
				projCommonGroup.AddProperty("LangVersion", "preview");
				projCommonGroup.AddProperty("AllowUnsafeBlocks", "true");
				projCommonGroup.AddProperty("EnableDefaultCompileItems", "false");
				projCommonGroup.AddProperty("EnableDefaultItems", "false");

				var refGroup = root.AddItemGroup();
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (assembly.IsDynamic)
						continue;

					string location;
					try
					{
						location = assembly.Location;
					}
					catch
					{
						continue;
					}

					if (string.IsNullOrEmpty(location) || !File.Exists(location))
						continue;

					var name = assembly.GetName().Name;
					if (string.IsNullOrEmpty(name))
						continue;

					if (name.StartsWith("System.", StringComparison.Ordinal))
						continue;

					if (!seen.Add(name))
						continue;

					var refElement = refGroup.AddItem("Reference", name);
					refElement.AppendChild(root.CreateMetadataElement("HintPath", location));
					refElement.AppendChild(root.CreateMetadataElement("Private", "false"));
				}

				var compileGroup = root.AddItemGroup();
				compileGroup.AddItem("Compile", Path.GetFileName(scriptFilePath));

				projFilePath = Path.Combine(genProjOutputDirPath, "Script.csproj");
				root.Save(projFilePath);

				return true;
			}
			catch (Exception e)
			{
				Log.LogError($"GenerateProjectFile failed: {e}");
				projFilePath = null;
				return false;
			}
		}

		public void Dispose()
		{
			WorkSpace?.Dispose();
			WorkSpace = default;
			Document = default;
			CompletionService = default;
		}
	}
}
