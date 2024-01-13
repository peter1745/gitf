using System;
using System.Collections.Generic;

namespace gitf.Commands;

internal class CreateCommitCommand : Command
{
	public override string GetName()
	{
		return "create";
	}

	public override bool ValidateData(CommandData data, ReadOnlySpan<string> args, out string error)
	{
		if (!base.ValidateData(data, args, out error))
		{
			return false;
		}
		
		if (args.IsEmpty)
		{
			error = "No name argument passed.";
			return false;
		}

		foreach (var commit in data.Project.Commits)
		{
			if (commit.Name == args[0])
			{
				error = $"Cannot create commit with name '{args[0]}', a commit with that name already exists.";
				return false;
			}
		}
		
		return true;
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		var commit = new CommitData()
		{
			Project = data.Project,
			Name = args[0]
		};

		var fileArgs = args.Length > 1 ? args.Slice(1) : ReadOnlySpan<string>.Empty;
		var files = CommandUtils.ParseArgumentFilePaths(fileArgs);

		int stagedFiles = 0;
		foreach (var file in files)
		{
			if (data.Database.StageFile(commit, file, out var message))
			{
				stagedFiles++;
			}
			
			messages.Add(message);
		}
		
		data.Project.Commits.Add(commit);

		messages.Add($"Created commit '{commit.Name}' and staged {stagedFiles} files.");
		return true;
	}
}