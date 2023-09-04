using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HFTProxy
{
	/// <summary>
	/// Config contains the listening port and destination.
	/// </summary>
	public class ConnectionInfo
	{
		public int ListeningPort { get; init; }
		public IPEndPoint DestinationAddress { get; init; }
		public IPAddress ViaIP { get; init; }
		public string Comment { get; init; }
        public ConnectionInfo(int listeningPort, IPEndPoint destinationAddress, IPAddress viaIP,string comment)
        {
			this.ListeningPort = listeningPort;
			this.DestinationAddress = destinationAddress;
			this.ViaIP = viaIP;
			this.Comment = comment;
		}
    }

	/// <summary>
	///	ConfigManager loads the config file and returns a list of Config objects.
	/// </summary>
	public class ConfigManager
	{
		public static List<ConnectionInfo> LoadConfig(string filePath)
		{
			List<ConnectionInfo> configs = new List<ConnectionInfo>();
			List<string> errors = new List<string>();
			string[] lines = File.ReadAllLines(filePath);

			foreach (var line in lines)
			{
				if (line.Trim().StartsWith("#")) continue;  // Skip comments

				string[] parts = line.Split(',');
				if (parts.Length <= 3)
				{
					errors.Add($"Invalid configuration line: {line}");
					continue;
				}

				string comment= parts.Length<4?string.Empty:parts[3];

				try
				{
					ConnectionInfo config = new ConnectionInfo(int.Parse(parts[0]), IPEndPoint.Parse(parts[1]), IPAddress.Parse(parts[2]), comment);
					configs.Add(config);
				}
				catch (Exception ex)
				{
					errors.Add($"Failed to parse line '{line}': {ex.Message}");
				}
			}

			if (errors.Any())
			{
				// Show all errors in a single message box
				MessageBox.Show($"The following errors were found in the configuration file:\n{string.Join("\n", errors)}. Config file was not loaded.");
			}

			return configs;
		}

	}
}
