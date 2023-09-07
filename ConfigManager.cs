using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;

namespace HFTProxy
{
	public class ConfigManager
	{
        public List<ConnectionInfo> LoadConfig(string filePath)
		{
			List<ConnectionInfo> configs = new List<ConnectionInfo>();
			List<string> errors = new List<string>();
			string[] lines = File.ReadAllLines(filePath);

			for (int i = 0; i < lines.Length; i++)
			{
				string? line = lines[i];
				if (line.Trim().StartsWith("#")) continue;  // Skip comments

				string[] parts = line.Split(',');
				if (parts.Length <= 3)
				{
					errors.Add($"Invalid configuration line number {i}.");
					continue;
				}

				string comment = parts.Length < 4 ? string.Empty : parts[3];

				try
				{
					ConnectionInfo config = new ConnectionInfo(int.Parse(parts[0]), IPEndPoint.Parse(parts[1]), IPAddress.Parse(parts[2]), comment);
					configs.Add(config);
				}
				catch (Exception ex)
				{
					errors.Add($"Failed to parse line {i}: {ex.Message}");
				}
			}

			var duplicatePorts = configs.GroupBy(x => x.ListeningPort).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
			if (duplicatePorts.Any())
			{
				errors.Add($"The following listening ports are duplicated: {string.Join(", ", duplicatePorts)}.");
				configs.Clear();
			}

			if (errors.Any())
			{
				// Show all errors in a single message box
				MessageBox.Show($"Config file was NOT loaded! The following errors were found in the configuration file:\n{string.Join("\n", errors)}.");
				configs.Clear();
			}

			return configs;
		}
	}
}
