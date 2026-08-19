using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Xml.Linq;
using System.Linq;

public class PostProcessVisualStudioCSProject : AssetPostprocessor
{
	static public void OnGeneratedCSProjectFiles()
	{
		var projectDirectory = Directory.GetParent(Application.dataPath).FullName; ;
		var projectName = Path.GetFileName(projectDirectory);
		var slnFile = Path.Combine(projectDirectory, $"{projectName}.sln");

		if (File.Exists(slnFile))
		{
			var slnText = File.ReadAllText(slnFile);
			using var sw = File.AppendText(slnFile);
			void WriteProject(string name, string path)
			{
				if (!slnText.Contains($"{name}.Git"))
				{
					var guid = Guid.NewGuid();
					var projTypeGuid = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
					sw.WriteLine($@"Project(""{projTypeGuid}"") = ""{name}.Git"", ""{path}\{name}.Git.csproj"", ""}}""");
					sw.WriteLine("EndProject");
				}
			}

			WriteProject("Unmath", "Packages/com.arycama.unmath");
			WriteProject("CustomRenderPipeline", "Packages/com.arycama.customrenderpipeline");
		}

		var slnxFile = Path.Combine(projectDirectory, $"{projectName}.slnx");
		if (File.Exists(slnxFile))
		{
			var doc = XDocument.Load(slnxFile);
			var isChanged = false;

			void WriteProject(string name, string path)
			{
				var projectPath = $"{path}/{name}.Git.csproj";
				var alreadyExists = doc.Root.Elements("Project").Any(e => e.Attribute("Path")?.Value == projectPath);
				if (!alreadyExists)
				{
					doc.Root.Add(new XElement("Project", new XAttribute("Path", projectPath)));
					isChanged = true;
				}
			}

			WriteProject("Unmath", "Packages/com.arycama.unmath");
			WriteProject("CustomRenderPipeline", "Packages/com.arycama.customrenderpipeline");

			if (isChanged)
				doc.Save(slnxFile);
		}
	}
}