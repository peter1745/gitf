using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace gitf.Commands;

internal partial class CheckpointCommand : Command
{
	public override string GetName()
	{
		return "checkpoint";
	}

	public override string GetShorthand()
	{
		return "chk";
	}

	public override string GetDescription()
	{
		return "Creates a new checkpoint or lists all the previous checkpoints.";
	}

	public override string GetUsage()
	{
		return "gitf checkpoint [--list | -l]";
	}

	public override bool ValidateData(CommandData commandData, ReadOnlySpan<string> args, out string error)
	{
		if (!base.ValidateData(commandData, args, out error))
		{
			return false;
		}

		if (args.IsEmpty)
		{
			return true;
		}

		if (args[0] == "--list" || args[0] == "-l")
		{
			if (args.Length > 1)
			{
				error = "Too many arguments.";
				return false;
			}

			return true;
		}
		
		if (args[0] == "--restore" || args[0] == "-r")
		{
			if (args.Length > 2)
			{
				error = "Too many arguments.";
				return false;
			}

			return true;
		}

		return false;
	}

	private void ListCheckpoints(CommandData data, ref List<string> messages)
	{
		foreach (var checkpoint in data.Project.Checkpoints)
		{
			var checkpointPath = data.Project.GetCheckpointPath(checkpoint);
			var fileCount = Directory.EnumerateFiles(checkpointPath, "", SearchOption.AllDirectories).Count();
				
			messages.Add($"Checkpoint {checkpoint} ({fileCount} files)");
		}
	}

	public override bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages)
	{
		if (!args.IsEmpty)
		{
			if (data.Project.Checkpoints.Count == 0)
			{
				messages.Add("No checkpoints have been created.");
				return true;
			}
			
			if (args[0] == "--list" || args[0] == "-l")
			{
				ListCheckpoints(data, ref messages);
			}
			else if (args[0] == "--restore" || args[0] == "-r")
			{
				int amount = 1;
					
				if (args.Length == 2 && !int.TryParse(args[1], out amount))
				{
					messages.Add($"Failed to parse {args[1]} as a number.");
					return false;
				}

				for (int i = 0; i < amount; i++)
				{
					if (data.Project.Checkpoints.Count == 0)
					{
						break;
					}

					data.Project.RestoreToLastCheckpoint(ref messages);
				}
			}
			
			return true;
		}

		var trackedFiles = CommandUtils.GetTrackedFiles();

		List<string> checkpointFiles = [];
            	
		foreach (var file in trackedFiles)
		{
			// Ignore directories and binary files
			if (Directory.Exists(file) || CommandUtils.IsBinaryFile(file))
			{
				continue;
			}
			
			checkpointFiles.Add(Path.GetFullPath(file));
		}
        
		int checkpointID = data.Project.Checkpoints.Count;
		var checkpointStorage = data.Project.GetCheckpointPath(checkpointID);
        
		if (string.IsNullOrEmpty(checkpointStorage))
		{
			messages.Add($"Failed to get checkpoint storage path for checkpoint id {checkpointID}. This shouldn't realistically happen.");
			return false;
		}
            	
		// If there's already a folder with this checkpoint id something has gone horribly wrong
		if (Directory.Exists(checkpointStorage))
		{
			messages.Add($"Failed to create checkpoint! Directory '{checkpointStorage}' already exists!");
			return false;
		}
        
		Directory.CreateDirectory(checkpointStorage);
        
		foreach (var file in checkpointFiles)
		{
			var destination = data.Project.GetCheckpointCopyDestination(checkpointID, file);
			var destinationFolder = Path.GetDirectoryName(destination);
            		
			messages.Add($"Adding {file}");
        
			if (!Directory.Exists(destinationFolder))
			{
				Directory.CreateDirectory(destinationFolder);
			}
            		
			File.Copy(file, destination);
		}
            	
		data.Project.Checkpoints.Add(checkpointID);
		
		messages.Add($"Created checkpoint with {checkpointFiles.Count} files");
		return true;
	}
}