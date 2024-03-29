﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Utils;

namespace gitf;

public static partial class CommandUtils
{
	
	public static List<string> ParseArgumentFilePaths(ReadOnlySpan<string> args)
	{
		List<string> files = [];
		files.EnsureCapacity(args.Length);

		Action<string> addFile = f => { files.Add(Path.GetFullPath(f)); };

		foreach (var filepath in args)
		{
			if (string.IsNullOrEmpty(filepath))
			{
				continue;
			}
			
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
    
	public static string RunCommand(string command)
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

	private static List<string> s_BinaryFileTypes =
	[
		".exe",
		".dll",
		".lib",
		".obj",
		".pdb",
		".ifc",
		".ddi",
		".ilk",
		".exp",
		".embed",
		".so",
		".a",
		".pch",
		".ttf",
		".ttc",
		".bin"
	];
	
	public static bool IsBinaryFile(string filepath)
	{
		return s_BinaryFileTypes.Contains(Path.GetExtension(filepath));
	}

	public static List<string> GetTrackedFiles()
	{
		List<string> result = [];

		var regex = GitLsFilesRegex();
		
		var trackedFiles = RunCommand("git ls-files -s");

		foreach (var line in trackedFiles.Split('\n'))
		{
			if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
			{
				continue;
			}
			
			if (!regex.IsMatch(line))
			{
				throw new Exception($"Failed to match {line} against regex!");
			}

			result.Add(regex.Replace(line, ""));
		}

		return result;
	}

    [GeneratedRegex(@"\d{6}\s[a-zA-Z0-9]{40}\s\d\s+")]
    private static partial Regex GitLsFilesRegex();
}