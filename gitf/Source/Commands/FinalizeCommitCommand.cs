using System;
using System.Collections.Generic;
using System.IO;
using static gitf.CommandUtils;

namespace gitf.Commands;

internal class FinalizeCommitCommand : Command
{
	public override string GetName()
	{
		return "commit";
	}
	
	public override bool ValidateData(CommandData data, ReadOnlySpan<string> args, out string error)
	{
		if (!base.ValidateData(data, args, out error))
		{
			return false;
		}

		if (data.Commit == null)
		{
			error = "Cannot finalize commit, no commit specified or found.";
			return false;
		}
		
		return true;
	}

	public override string GetDescription()
	{
		return "Finalizes a commit and generates a git commit.";
	}
	
	public override string GetUsage()
	{
		return "gitf commit <commit_name> [git_commit_args]";
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		// Figure out which branch is currently checked out (we'll switch back to this branch after committing)
		string branch = RunCommand("git branch");
		int branchStart = branch.IndexOf('*') + 2;
		int branchEnd = branch.IndexOf('\n', branchStart);
		branch = branch.Substring(branchStart, branchEnd - branchStart).Trim();

		// Stash all current changes
		string stashName = $"gitf-{data.Commit.Name}";
		RunCommand($"git stash push -m {stashName}");
		bool stashedChanges = RunCommand($"git stash list").Contains(stashName);

		// Make backups of the source files that *we* track in case they weren't stashed
		foreach (var file in data.Commit.Files)
		{
			// Copy the staged file to the source file location
			File.Copy(data.Project.GetStoragePath(file), file, true);
		}

		string tempBranch = $"gitf-{data.Commit.Name}";

		// Create a new branch that we can commit our tracked files to
		_ = RunCommand($"git checkout -b {tempBranch}");

		foreach (var file in data.Commit.Files)
		{
			_ = RunCommand($"git add {file}");
		}

		string gitArgs = string.Empty;
		
		foreach (var arg in args.Slice(1))
		{
			if (arg.Contains(' '))
			{
				gitArgs += $"\"{arg}\" ";
			}
			else
			{
				gitArgs += arg;
			}
		}

		gitArgs = gitArgs.TrimEnd();

		if (string.IsNullOrEmpty(gitArgs))
			_ = RunCommand("git commit");
		else
			_ = RunCommand($"git commit {gitArgs}");

		// TODO: Option for checking out a different branch for merging
		_ = RunCommand($"git checkout {branch}");

		if (stashedChanges)
		{
			_ = RunCommand("git stash pop");
		}

		// Merge the gitf branch into the current branch
		_ = RunCommand($"git merge {tempBranch}");

		// Delete the "feature" branch
		_ = RunCommand($"git branch -d {tempBranch}");

		// Delete the gitf commit and it's cached files
		foreach (var file in data.Commit.Files)
		{
			File.Delete(data.Project.GetStoragePath(file));
		}
		
		data.Project.Commits.Remove(data.Commit);
		
		return true;
	}
}