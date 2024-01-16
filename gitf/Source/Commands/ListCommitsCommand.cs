using System;
using System.Collections.Generic;

namespace gitf.Commands;

internal class ListCommitsCommand : Command
{
	public override string GetName()
	{
		return "list";
	}

	public override string GetDescription()
	{
		return "Lists all commits in this project.";
	}

	public override string GetUsage()
	{
		return "gitf list";
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		foreach (var commit in data.Project.Commits)
		{
			messages.Add($"{commit.Name} ({commit.Files.Count} files)");
		}

		return true;
	}
}