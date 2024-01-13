using System;
using System.Collections.Generic;

namespace gitf.Commands;

internal class StageFileCommand : Command
{
	public override string GetName()
	{
		return "stage";
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
			error = "Cannot stage files, no commit specified or found.";
			return false;
		}
		
		return true;
	}

	public override string GetDescription()
	{
		return "Stages one or multiple files into a commit.";
	}

	public override string GetUsage()
	{
		return "gitf stage <commit_name> [<file1> <file2> <...>]";
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		var fileArgs = args.Length > 1 ? args.Slice(1) : ReadOnlySpan<string>.Empty;
		var files = CommandUtils.ParseArgumentFilePaths(fileArgs);

		int stagedFiles = 0;
		
		foreach (var file in files)
		{
			if (data.Database.StageFile(data.Commit, file, out var message))
			{
				stagedFiles++;
			}
			
			messages.Add(message);
		}

		messages.Add($"Staged {stagedFiles} files");
		return true;
	}
}