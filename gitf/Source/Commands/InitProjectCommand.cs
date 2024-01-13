using System;
using System.Collections.Generic;
using System.IO;

namespace gitf.Commands;

internal class InitProjectCommand : Command
{
	public override string GetName()
	{
		return "init";
	}

	public override bool ValidateData(CommandData commandData, ReadOnlySpan<string> args, out string error)
	{
		if (commandData.Project != null)
		{
			error = "Cannot call 'gitf init' in current folder, project already exists.";
			return false;
		}
		
		error = string.Empty;
		return true;
	}

	public override string GetDescription()
	{
		return "Initializes a new gitf project in the current folder.";
	}
    
	public override string GetUsage()
	{
		return "gitf init";
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		string projectName = args.IsEmpty ? Path.GetFileName(data.ProjectDirectory) : args[0];

		if (string.IsNullOrEmpty(projectName))
		{
			messages.Add("No project name specified.");
			return false;
		}

		var project = new CommitProject()
		{
			Name = projectName,
			FilePath = data.ProjectDirectory
		};

		_ = Directory.CreateDirectory(Path.Combine(CommitDB.s_StoragePath, project.Name));

		data.Database.Projects.Add(project);

		messages.Add($"Initialized project '{project.Name}' in folder {project.FilePath}");
		return true;
	}
}
	