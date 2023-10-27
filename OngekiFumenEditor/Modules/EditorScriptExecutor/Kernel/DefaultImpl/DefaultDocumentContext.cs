using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
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
			foreach (var item in result.Items)
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
			var root = ProjectRootElement.Create();
			root.Sdk = "Microsoft.NET.Sdk";

			var projCommonGroup = root.AddPropertyGroup();
			projCommonGroup.AddProperty("TargetFramework", "net6.0-windows");
			projCommonGroup.AddProperty("OutputType", "Exe");


			var refGroup = root.AddItemGroup();
			//add references
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var name = assembly.GetName().Name;

				//filter System.*
				if (name.StartsWith("System."))
					continue;

				try
				{
					if (!File.Exists(assembly.Location))
						continue;
				}
				catch
				{
					//in-memory assembly will throw NotSupportException
					continue;
				}

				var refElement = refGroup.AddItem("Reference", name);

				refElement.AppendChild(root.CreateMetadataElement("HintPath", assembly.Location));
				refElement.AppendChild(root.CreateMetadataElement("Private", "false"));
			}

			projFilePath = Path.Combine(genProjOutputDirPath, "Script.csproj");
			root.Save(projFilePath);

			return true;
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
