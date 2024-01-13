using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

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
}

public class CommitDB
{
	public static readonly string s_StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gitf");
	public static readonly string s_DatabasePath = Path.Combine(s_StoragePath, "db.json");

	public List<CommitProject> Projects = [];
	
	public bool StageFile(CommitData commit, string file, out string message)
	{
		if (!File.Exists(file))
		{
			message = $"Cannot stage file {file} because it doesn't exist.";
			return false;
		}

		var destinationFile = commit.Project.GetStoragePath(file);
		var destinationFolder = Path.GetDirectoryName(destinationFile);

		if (string.IsNullOrEmpty(destinationFolder))
		{
			message = $"Failed to get directory name for path {file}";
			return false;
		}
		
		_ = Directory.CreateDirectory(destinationFolder);

		File.Copy(file, destinationFile, true);

		_ = CommandUtils.RunCommand($"git restore {file}");

		// Only add the file to the json file if it isn't already in there (still want to copy the newest version of the file though)
		if (!commit.Files.Contains(file))
			commit.Files.Add(file);

		message = $"Staged file {file}";
		return true;
	}
	
	public bool UnstageFile(CommitData commit, string file, out string message)
	{
		var stagedFile = commit.Project.GetStoragePath(file);

		if (!File.Exists(stagedFile) || !commit.Files.Remove(file))
		{
			message = $"Cannot unstage file {file} because it wasn't previously staged.";
			return false;
		}
		
		File.Delete(stagedFile);

		message = $"Unstaged {file}";
		return true;
	}
}