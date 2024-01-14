using System;
using System.Collections.Generic;

namespace gitf.Commands;

internal class DeleteCommitCommand : Command
{
	public override string GetName()
	{
		return "delete";
	}
	
	public override bool ValidateData(CommandData data, ReadOnlySpan<string> args, out string error)
	{
		if (!base.ValidateData(data, args, out error))
		{
			return false;
		}
		
		if (args.IsEmpty)
		{
			error = "Not enough arguments specified.";
			return false;
		}

		if (data.Commit == null)
		{
			error = "Cannot delete commit, commit not specified or found.";
			return false;
		}
		
		return true;
	}

	public override string GetDescription()
	{
		return "Deletes a commit permanently.";
	}
	
	public override string GetUsage()
	{
		return "gitf delete <commit_name>";
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		Console.Write($"Are you sure you want to delete {data.Commit.Name}? [Y/n] ");

		var response = Console.ReadLine();

		if (string.IsNullOrEmpty(response) || response.ToLower() == "y")
		{
			ReadOnlySpan<string> files = new(data.Commit.Files.ToArray());
			
			foreach (var file in files)
			{
				_ = data.Project.UnstageFile(data.Commit, file, out _);
			}

			data.Project.Commits.Remove(data.Commit);
			messages.Add($"Deleted commit {data.Commit.Name}");
		}
		else
		{
			messages.Add("Aborted...");
		}
		
		return true;
	}
}