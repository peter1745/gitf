using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.TemplateEngine.Utils;

public class Program
{

	private class CommitData
	{
		public string Name;
		public List<string> Files = [];
	}

	private class CommitProject
	{
		public string Name;
		public string FilePath;

		public List<CommitData> Commits = [];
	}

	private class CommitDB
	{
		public List<CommitProject> Projects = [];

		[JsonIgnore]
		public CommitProject ActiveProject;
	}

	private static readonly string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gitf");
	private static readonly string DatabasePath = Path.Combine(StoragePath, "db.json");

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		IncludeFields = true,
	};

	private static void InitProject(CommitDB db)
	{
		if (db.ActiveProject != null)
		{
			Console.WriteLine($"Cannot init {Environment.CurrentDirectory}, it's already registered in the database");
			return;
		}

		var project = new CommitProject()
		{
			Name = Path.GetFileName(Environment.CurrentDirectory),
			FilePath = Environment.CurrentDirectory
		};

		Console.WriteLine($"Initializing project {project.Name} ({project.FilePath})");

		Directory.CreateDirectory(Path.Combine(StoragePath, project.Name));
		db.Projects.Add(project);

		SerializeDatabase(db);
	}

	private static List<string> ParseArgumentFilePaths()
	{
		var args = Environment.GetCommandLineArgs();
		var filepaths = new ReadOnlySpan<string>(args, 3, args.Length - 3);

		List<string> files = new(filepaths.Length);

		Action<string> addFile = f => { files.Add(Path.GetFullPath(f)); };

		foreach (var filepath in filepaths)
		{
			if (Directory.Exists(filepath))
			{
				Directory
					.EnumerateFiles(filepath, string.Empty, SearchOption.AllDirectories)
					.ForEach(addFile);

				continue;
			}

			if (!filepath.Contains('*') && !filepath.Contains("**"))
			{
				addFile(filepath);
				continue;
			}

			bool iterateSubdirectories = filepath.Contains("**");
			var searchOptions = iterateSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			Directory
				.EnumerateFiles(Environment.CurrentDirectory, filepath, searchOptions)
				.ForEach(addFile);
		}

		return files;
	}

	private static string GetDatabaseRelativePath(CommitDB db, string filepath)
	{
		var relativePath = Path.GetDirectoryName(Path.GetRelativePath(db.ActiveProject.FilePath, filepath));
		var copyDestination = Path.Combine(StoragePath, db.ActiveProject.Name, relativePath);
		return Path.Combine(copyDestination, Path.GetFileName(filepath));
	}

	private static void AddFileToCommit(CommitDB db, CommitData commitData, string originalFilePath)
	{
		if (db.ActiveProject == null || string.IsNullOrEmpty(db.ActiveProject.Name))
		{
			Console.WriteLine($"No gitf project in folder {Environment.CurrentDirectory}");
			return;
		}

		if (!File.Exists(originalFilePath))
		{
			Console.WriteLine($"Cannot stage file {originalFilePath} because it doesn't exist.");
			return;
		}

		var destinationFile = GetDatabaseRelativePath(db, originalFilePath);
		var destinationFolder = Path.GetDirectoryName(destinationFile);
		_ = Directory.CreateDirectory(destinationFolder);

		File.Copy(originalFilePath, destinationFile, true);

		_ = RunCommand($"git restore {originalFilePath}");

		// Only add the file to the json file if it isn't already in there (still want to copy the newest version of the file though)
		if (!commitData.Files.Contains(originalFilePath))
			commitData.Files.Add(originalFilePath);
	}

	private static void CreateCommit(CommitDB db)
	{
		if (db.ActiveProject == null || string.IsNullOrEmpty(db.ActiveProject.Name))
		{
			Console.WriteLine($"No gitf project in folder {Environment.CurrentDirectory}");
			return;
		}

		var commit = new CommitData();

		var args = Environment.GetCommandLineArgs();

		if (args.Length < 3)
		{
			Console.WriteLine("Cannot create a commit without giving it a name.");
			return;
		}

		commit.Name = args[2];

		foreach (var c in db.ActiveProject.Commits)
		{
			if (c.Name == commit.Name)
			{
				Console.WriteLine($"Cannot create commit with name {commit.Name}, it already exists.");
				return;
			}
		}

		var files = ParseArgumentFilePaths();

		foreach (var file in files)
		{
			AddFileToCommit(db, commit, file);
		}

		Console.WriteLine($"Created new commit {commit.Name} and staged {commit.Files.Count} files");
		db.ActiveProject.Commits.Add(commit);
		SerializeDatabase(db);
	}

	private static void StageFiles(CommitDB db)
	{
		if (db.ActiveProject == null || string.IsNullOrEmpty(db.ActiveProject.Name))
		{
			Console.WriteLine($"No gitf project in folder {Environment.CurrentDirectory}");
			return;
		}

		var args = Environment.GetCommandLineArgs();
		var name = args[2];

		CommitData commitData = null;

		foreach (var commit in db.ActiveProject.Commits)
		{
			if (commit.Name == name)
			{
				commitData = commit;
				break;
			}
		}

		if (commitData == null)
		{
			Console.WriteLine($"Cannot stage files to commit {name} because there are no commits with that name");
			return;
		}

		var files = ParseArgumentFilePaths();

		foreach (var file in files)
		{
			AddFileToCommit(db, commitData, file);
			Console.WriteLine($"Staged {file}");
		}

		Console.WriteLine($"Staged {files.Count} files");
		SerializeDatabase(db);
	}

	private static void UnstageFiles(CommitDB db)
	{
		if (db.ActiveProject == null || string.IsNullOrEmpty(db.ActiveProject.Name))
		{
			Console.WriteLine($"No gitf project in folder {Environment.CurrentDirectory}");
			return;
		}

		var args = Environment.GetCommandLineArgs();
		var name = args[2];

		CommitData commitData = null;

		foreach (var commit in db.ActiveProject.Commits)
		{
			if (commit.Name == name)
			{
				commitData = commit;
				break;
			}
		}

		if (commitData == null)
		{
			Console.WriteLine($"Cannot unstage files for commit {name} because there are no commits with that name");
			return;
		}

		var files = ParseArgumentFilePaths();

		foreach (var file in files)
		{
			if (commitData.Files.Remove(file))
			{
				var fileToDelete = GetDatabaseRelativePath(db, file);
				File.Delete(fileToDelete);

				Console.WriteLine($"Unstaged {file}");
			}
		}

		Console.WriteLine($"Unstaged {files.Count} files");
		SerializeDatabase(db);
	}

	private static void DeleteCommit(CommitDB db)
	{
		if (db.ActiveProject == null || string.IsNullOrEmpty(db.ActiveProject.Name))
		{
			Console.WriteLine($"No gitf project in folder {Environment.CurrentDirectory}");
			return;
		}

		var args = Environment.GetCommandLineArgs();
		var name = args[2];

		CommitData commitData = null;

		foreach (var commit in db.ActiveProject.Commits)
		{
			if (commit.Name == name)
			{
				commitData = commit;
				break;
			}
		}

		if (commitData == null)
		{
			Console.WriteLine($"No commit called '{name}'");
			return;
		}

		Console.Write($"Are you sure want to delete {name}? [Y/n] ");

		var input = Console.ReadLine();

		if (string.IsNullOrEmpty(input) || input.ToUpper() == "Y")
		{
			// Proceed with deletion
			foreach (var file in commitData.Files)
			{
				var fileToDelete = GetDatabaseRelativePath(db, file);
				File.Delete(fileToDelete);
			}

			db.ActiveProject.Commits.Remove(commitData);
			SerializeDatabase(db);
		}
		else
		{
			Console.WriteLine("Aborted...");
		}
	}

	private static string RunCommand(string command)
	{
		var startInfo = new ProcessStartInfo()
		{
			WindowStyle = ProcessWindowStyle.Hidden,
			FileName = "cmd.exe",
			Arguments = "/C " + command,
			WorkingDirectory = Environment.CurrentDirectory,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		var process = new Process()
		{
			StartInfo = startInfo
		};

		process.Start();

		string output = process.StandardOutput.ReadToEnd();

		process.WaitForExit();

		return output;
	}

	private static void FinalizeCommit(CommitDB db)
	{
		if (db.ActiveProject == null || string.IsNullOrEmpty(db.ActiveProject.Name))
		{
			Console.WriteLine($"No gitf project in folder {Environment.CurrentDirectory}");
			return;
		}

		var args = Environment.GetCommandLineArgs();
		var name = args[2];

		CommitData commitData = null;

		foreach (var commit in db.ActiveProject.Commits)
		{
			if (commit.Name == name)
			{
				commitData = commit;
				break;
			}
		}

		if (commitData == null)
		{
			Console.WriteLine($"No commit called '{name}'");
			return;
		}

		// Figure out which branch is currently checked out (we'll switch back to this branch after committing)
		string branch = RunCommand("git branch");
		int branchStart = branch.IndexOf('*') + 2;
		int branchEnd = branch.IndexOf('\n', branchStart);
		branch = branch.Substring(branchStart, branchEnd - branchStart).Trim();

		// Stash all current changes
		string stashName = $"gitf-{commitData.Name}";
		RunCommand($"git stash push -m {stashName}");
		bool stashedChanges = RunCommand($"git stash list").Contains(stashName);

		// Make backups of the source files that *we* track in case they weren't stashed
		foreach (var file in commitData.Files)
		{
			var dbFile = GetDatabaseRelativePath(db, file);

			// NOTE(Peter): This code may become relevant under certain circumstances (haven't encountered those yet, but keeping it here just in case)
			//var dbRelative = Path.GetRelativePath(StoragePath, dbFile);
			//var backupDir = Path.Combine(StoragePath, "staging", Path.GetDirectoryName(dbRelative));
			//var backupFile = Path.Combine(StoragePath, "staging", dbRelative);

			//_ = Directory.CreateDirectory(backupDir);

			// Create backup of the source file
			//File.Copy(file, backupFile, true);

			// Copy the staged file to the source file location
			File.Copy(dbFile, file, true);
		}

		string tempBranch = $"gitf-{commitData.Name}";

		// Create a new branch that we can commit our tracked files to
		_ = RunCommand($"git checkout -b {tempBranch}");

		foreach (var file in commitData.Files)
		{
			_ = RunCommand($"git add {file}");
		}

		var gitArgs = string.Join(" ", args, 3, args.Length - 3);

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

		_ = RunCommand($"git branch -d {tempBranch}");
	}

	private static void SerializeDatabase(CommitDB db)
	{
		File.WriteAllText(DatabasePath, JsonSerializer.Serialize(db, JsonOptions));
	}

	public static void Main(string[] args)
	{
		/*
		
		gitf create <name> <file1> <file2> ...
		gitf stage <name> <file1> <file2> ...
		gitf unstage <name> <file1> <file2> ...
		gitf delete <name>
		gitf commit <name> -m "Here's my message"

		gitf checkpoint [--list | -l]
		gitf restore [<amount>]

		*/

		if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
		{
			// TODO: Show help menu
			return;
		}

		if (!Directory.Exists(StoragePath))
		{
			Directory.CreateDirectory(StoragePath);
		}

		CommitDB db;

		if (File.Exists(DatabasePath))
		{
			db = JsonSerializer.Deserialize<CommitDB>(File.ReadAllText(DatabasePath), JsonOptions);
		}
		else
		{
			db = new CommitDB();
		}

		if (db == null)
		{
			Console.WriteLine("An unknown error occured. Failed to read or create commit database.");
			return;
		}

		foreach (var project in db.Projects)
		{
			if (project.FilePath == Environment.CurrentDirectory)
			{
				db.ActiveProject = project;
				break;
			}
		}

		string action = args[0];

		/*
		Commit Time:
			1. Create branch
			2. Create a git commit in the new branch
			3. Merge the commit branch into the current branch (option for merging into a different branch)
			4. (Optional, Preferred) Automatically delete the commit branch after successfull merge
		 */
		switch (action)
		{
			case "init":
				InitProject(db);
				break;
			case "create":
				CreateCommit(db);
				break;
			case "stage":
				StageFiles(db);
				break;
			case "unstage":
				UnstageFiles(db);
				break;
			case "delete":
				DeleteCommit(db);
				break;
			case "commit":
				FinalizeCommit(db);
				break;
		}
	}
}
