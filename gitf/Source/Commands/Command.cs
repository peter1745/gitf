using System;
using System.Collections.Generic;

namespace gitf.Commands
{
	internal abstract class Command
	{
		internal struct CommandData
		{
			public CommitDB Database { get; init; }
			public CommitProject Project { get; init; }
			public CommitData Commit { get; init; }
			public string ProjectDirectory {  get; init; }
		}

		/// <summary>
		/// Validates that the provided data is correct for this command.
		/// Checks the state of the program (e.g that we have an active project for commands that require it) as
		/// well as validating that the arguments passed are valid for this command.
		/// Called before executing the command.
		/// </summary>
		/// <param name="commandData">Contains the command-relevant program state (e.g CommitDB)</param>
		/// <param name="args">The command-specific arguments from the command line</param>
		/// <param name="error">An error message that will be printed if this method returns false</param>
		/// <returns>true if the given data is valid for this command, otherwise false</returns>
		public virtual bool ValidateData(CommandData commandData, ReadOnlySpan<string> args, out string error)
		{
			if (commandData.Project == null || string.IsNullOrEmpty(commandData.Project.Name))
			{
				error = "No gitf project active in current folder.";
				return false;
			}
			
			error = string.Empty;
			return true;
		}

		public abstract string GetName();

		public virtual string GetShorthand()
		{
			return null;
		}
		
		public abstract string GetDescription();
		public abstract string GetUsage();
		public abstract bool Execute(CommandData data, ReadOnlySpan<string> args, ref List<string> messages);
	}
}
