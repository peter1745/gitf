using System;
using System.Collections.Generic;

namespace gitf.Commands;

internal class UnstageFileCommand : Command
{
	public override string GetName()
	{
		return "unstage";
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
			error = "Cannot unstage files, no commit specified or found.";
			return false;
		}
		
		return true;
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		var fileArgs = args.Length > 1 ? args.Slice(1) : ReadOnlySpan<string>.Empty;
		var files = CommandUtils.ParseArgumentFilePaths(fileArgs);

		int unstagedFiles = 0;
		
		foreach (var file in files)
		{
			if (data.Database.UnstageFile(data.Commit, file, out var message))
			{
				unstagedFiles++;
			}
			
			messages.Add(message);
		}

		messages.Add($"Unstaged {unstagedFiles} files");
		return true;
	}
}