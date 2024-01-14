using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using gitf.Commands;

namespace gitf;

public class CommitData
{
	[JsonIgnore] public CommitProject Project;
	public string Name;
	
	// NOTE(Peter): Don't make this readonly, it breaks the JSON deserializer
	public List<string> Files = [];
}

public class CommitProject
{
	public string Name;
	public string FilePath;

	// NOTE(Peter): Don't make this readonly, it breaks the JSON deserializer
	public List<CommitData> Commits = [];

	public List<int> Checkpoints = [];
	
	public string GetStoragePath(string filepath)
	{
		var relativePath = Path.GetDirectoryName(Path.GetRelativePath(FilePath, filepath));

		if (relativePath == null)
		{
			Console.WriteLine($"Failed to get relative path for {filepath}");
			return string.Empty;
		}
		
		var copyDestination = Path.Combine(CommitDB.s_StoragePath, Name, relativePath);
		return Path.Combine(copyDestination, Path.GetFileName(filepath));
	}

	public string GetCheckpointPath(int checkpointID)
	{
		return GetStoragePath(Path.Combine("checkpoints", checkpointID.ToString()));
	}
	
	public string GetCheckpointCopyDestination(int checkpointID, string filepath)
	{
		var relativePath = Path.GetDirectoryName(Path.GetRelativePath(FilePath, filepath));

		if (relativePath == null)
		{
			Console.WriteLine($"Failed to get relative path for {filepath}");
			return string.Empty;
		}
		
		var copyDestination = Path.Combine(GetCheckpointPath(checkpointID), relativePath);
		return Path.Combine(copyDestination, Path.GetFileName(filepath));
	}
	
	public bool StageFile(CommitData commit, string file, out string message)
	{
		if (!File.Exists(file))
		{
			message = $"Cannot stage file {file} because it doesn't exist.";
			return false;
		}

		var destinationFile = GetStoragePath(file);
		var destinationFolder = Path.GetDirectoryName(destinationFile);

		if (string.IsNullOrEmpty(destinationFolder))
		{
			message = $"Failed to get directory name for path {file}";
			return false;
		}
		
		_ = Directory.CreateDirectory(destinationFolder);

		File.Copy(file, destinationFile, true);

		// Check if this file is part of a checkpoint, and restore to the checkpoint if that's the case
		if (Checkpoints.Count > 0)
		{
			List<string> messages = [];
			RestoreToLastCheckpoint(ref messages);
		}
		else
		{
			_ = CommandUtils.RunCommand($"git restore {file}");
		}

		// Only add the file to the json file if it isn't already in there (still want to copy the newest version of the file though)
		if (!commit.Files.Contains(file))
			commit.Files.Add(file);

		message = $"Staged file {file}";
		return true;
	}
	
	public bool UnstageFile(CommitData commit, string file, out string message)
	{
		var stagedFile = GetStoragePath(file);

		if (!File.Exists(stagedFile) || !commit.Files.Remove(file))
		{
			message = $"Cannot unstage file {file} because it wasn't previously staged.";
			return false;
		}
		
		File.Delete(stagedFile);

		message = $"Unstaged {file}";
		return true;
	}
	
	public void RestoreToLastCheckpoint(ref List<string> messages)
	{
		if (Checkpoints.Count == 0)
		{
			return;
		}
		
		int checkpointID = Checkpoints[Index.FromEnd(1)];
		var checkpointPath = GetCheckpointPath(checkpointID);
		
		if (!Directory.Exists(checkpointPath))
		{
			messages.Add($"Cannot restore to checkpoint {checkpointID}, no folder for that checkpoint exists.");
			return;
		}

		messages.Add($"Restoring to checkpoint {checkpointID}");
		
		foreach (var file in Directory.EnumerateFiles(checkpointPath, "", SearchOption.AllDirectories))
		{
			var relativePath = Path.GetRelativePath(checkpointPath, file);
			var targetPath = Path.Combine(FilePath, relativePath);
			
			File.Copy(file, targetPath, true);
			
			messages.Add($"Restored {targetPath}");
		}
		
		Directory.Delete(checkpointPath, true);
		Checkpoints.RemoveAt(Checkpoints.Count - 1);
	}
}

public class CommitDB
{
	public static readonly string s_StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gitf");
	public static readonly string s_DatabasePath = Path.Combine(s_StoragePath, "db.json");

	public List<CommitProject> Projects = [];
}