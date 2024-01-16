using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using gitf.Commands;

namespace gitf;

public class Program
{
	private static readonly JsonSerializerOptions s_DatabaseJsonOptions = new()
	{
		IncludeFields = true
	};
	
	private readonly List<Command> m_Commands = [];

	private CommitDB m_Database;
	private CommitProject m_ActiveProject;

	private Program()
	{
		RegisterCommands();
		LoadDatabase();
	}
	
	private void RegisterCommands()
	{
		try
		{
			var assembly = typeof(Program).Assembly;

			foreach (var type in assembly.GetTypes())
			{
				if (!type.IsSubclassOf(typeof(Command)))
				{
					continue;
				}

				var command = Activator.CreateInstance(type) as Command;

				if (command == null)
				{
					Console.WriteLine($"Failed to create instance of command {type}");
					continue;
				}
				
				m_Commands.Add(command);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
			Environment.Exit(-1);
		}
	}

	private void LoadDatabase()
	{
		if (!Directory.Exists(CommitDB.s_StoragePath))
		{
			Directory.CreateDirectory(CommitDB.s_StoragePath);
		}

		m_Database = File.Exists(CommitDB.s_DatabasePath)
			? JsonSerializer.Deserialize<CommitDB>(File.ReadAllText(CommitDB.s_DatabasePath), s_DatabaseJsonOptions)
			: new CommitDB();

		Debug.Assert(m_Database != null, "Failed to load or create CommitDB!");
        
		foreach (var project in m_Database.Projects)
		{
			if (project.FilePath == Environment.CurrentDirectory)
			{
				m_ActiveProject = project;
				break;
			}
		}

		if (m_ActiveProject != null)
		{
			m_ActiveProject.TrackedFiles = CommandUtils.GetTrackedFiles();
		}
	}
	
	private void Execute(ReadOnlySpan<string> args)
	{
		if (args.IsEmpty || args[0] == "--help" || args[0] == "-h")
		{
			PrintHelpInfo();
			return;
		}

		string requestedCommand = args[0];

		Command command = null;
		
		foreach (var cmd in m_Commands)
		{
			if (requestedCommand == cmd.GetName() || requestedCommand == cmd.GetShorthand())
			{
				command = cmd;
				break;
			}
		}
		
		if (command == null)
		{
			Console.WriteLine($"gitf: '{requestedCommand}' is not a gitf command. See 'gitf --help' for more info.");
			return;
		}

		var commandArgs = args.Length > 1 ? args.Slice(1) : ReadOnlySpan<string>.Empty;
		
		// Try to find a commit that has a name that matches commandArgs[0] (do this here because it's a common operation for a lot of commands)
		CommitData activeCommit = null;
		
		if (m_ActiveProject != null && !commandArgs.IsEmpty)
		{
			foreach (var commit in m_ActiveProject.Commits)
			{
				if (commit.Name == commandArgs[0])
				{
					activeCommit = commit;
					break;
				}
			}
		}

		var commandData = new Command.CommandData()
		{
			Database = m_Database,
			Project = m_ActiveProject,
			Commit = activeCommit,
			ProjectDirectory = m_ActiveProject == null ? Environment.CurrentDirectory : m_ActiveProject.FilePath,
		};

		if (!command.ValidateData(commandData, commandArgs, out var error))
		{
			Console.WriteLine($"Invalid arguments passed to 'gitf {requestedCommand}'.");
			Console.WriteLine(error);
			return;
		}

		List<string> messages = [];

		if (!command.Execute(commandData, commandArgs, ref messages))
		{
			Console.WriteLine($"'gitf {requestedCommand}' failed.");
		}
		else
		{
			File.WriteAllText(CommitDB.s_DatabasePath, JsonSerializer.Serialize(m_Database, s_DatabaseJsonOptions));
		}

		foreach (var message in messages)
		{
			Console.WriteLine(message);
		}
	}

	private void PrintHelpInfo()
	{
		Console.WriteLine("Usage: gitf <action> [<options>]");
		Console.WriteLine();

		Console.WriteLine("ACTIONS");

		const int NameAlignmentFromStart = 4;
		const int UsageAlignmentFromStart = NameAlignmentFromStart + 2;
		
		foreach (var command in m_Commands)
		{
			var commandName = command.GetName();
			
			for (int i = 0; i < NameAlignmentFromStart; i++)
				Console.Write(" ");

			var commandShorthand = command.GetShorthand();
			
			Console.WriteLine($"{commandName}:");

			for (int i = 0; i < UsageAlignmentFromStart; i++)
				Console.Write(" ");

			Console.WriteLine($"{command.GetDescription()}");

			if (!string.IsNullOrEmpty(commandShorthand))
			{
				for (int i = 0; i < UsageAlignmentFromStart; i++)
					Console.Write(" ");
				
				Console.WriteLine($"Alias: {commandShorthand}");
			}
			
			for (int i = 0; i < UsageAlignmentFromStart; i++)
				Console.Write(" ");
			
			Console.WriteLine($"Usage: {command.GetUsage()}");
			Console.WriteLine();
		}
	}

	public static void Main(string[] args)
	{
		new Program().Execute(args);
	}
}
