using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HFTProxy
{
	/// <summary>
	/// This is a NET6 WPF application.
	/// HFTProxy is a proxy, created for high frequency trading. It listens on localhost port and forwards data to destination address found in config.txt.
	/// It is designed to handle multiple connections, and transfer data between them as fast as possible.
	/// Config contains the listening port and destination and which IP (ViaIP) to use for external communication.
	/// The external TCP connection can only be created once there is an incoming connection on the listening port.
	/// The external TCP connections are usually kept alive for hours or days.
	/// Start button will start the proxy, and stop button will stop it.
	/// Refresh button will reload the config file, but keep existing connections.
	/// The active connections grid shows the connections that are currently active.
	/// It is running on a powerfull machine, brandwith is not an issue. Latency is the ultimate priority.
	///

	/// 
	/// TODO:
	/// Config file shold be validated before loading. In case of errors, the config file should not be loaded and error message should be displayed in a message box.
	/// No ConnectionInfo should have the same listening port. It it happens on loadfile, it should be reported as an error, and the config file should not be loaded.
	/// If it is a refresh, then the existing connection with that listening port should be closed, and the new one should be created according to the new config.
	/// The active listening ports should be stored somewhere, to detect if a new connection is a refresh or a new connection.
	/// </summary>
	/// 
	public class ErrorHandler 
	{
		public void AddException(Exception ex, string function)
		{
			//TODO
		}
		public void AddError(string message) 
		{ 
			//TODO
		}
	}

	public class ConnectionInfo
	{
		public int ListeningPort { get; init; }
		public IPEndPoint DestinationAddress { get; init; }
		public IPAddress ViaIP { get; init; }
		public string Comment { get; init; }
		public ConnectionInfo(int listeningPort, IPEndPoint destinationAddress, IPAddress viaIP, string comment)
		{
			this.ListeningPort = listeningPort;
			this.DestinationAddress = destinationAddress;
			this.ViaIP = viaIP;
			this.Comment = comment;
		}
	}

	public class ConfigManager
	{
        public static List<ConnectionInfo> LoadConfig(string filePath)
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

	public class MainWindowViewModel:INotifyPropertyChanged
	{
		string selectedConfigFilePath = string.Empty;
		public string SelectedConfigFilePath
		{
			get { return selectedConfigFilePath; }
			set
			{
				selectedConfigFilePath = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedConfigFilePath)));
			}
		}
		public event PropertyChangedEventHandler? PropertyChanged;

		public ObservableCollection<ConnectionInfo> ActiveConnections { get; set; } = new ObservableCollection<ConnectionInfo>();
		
		public MainWindowViewModel()
		{
			if (IsInDesignMode()) // Egy egyszerű függvény, amely ellenőrzi, hogy design módban vagyunk-e.
			{
				var dumm1 = new ConnectionInfo(8080, new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080), IPAddress.Parse("192.168.1.2"), "blah");
				var dummy2 = new ConnectionInfo(8081, new IPEndPoint(IPAddress.Parse("192.168.1.2"), 8080), IPAddress.Parse("192.168.1.3"), "blahblah");
				ActiveConnections.Add(dumm1);
				ActiveConnections.Add(dummy2);
				SelectedConfigFilePath= @"C:\Users\Public\Documents\config.txt";
			}
			else
			{
				// Runtime inicializáció
			}
		}

		private bool IsInDesignMode()
		{
			return DesignerProperties.GetIsInDesignMode(new DependencyObject());
		}
	}


	public partial class MainWindow : Window
	{
		ErrorHandler errorHandler = new ErrorHandler();
		private readonly MainWindowViewModel viewModel;
		private CancellationTokenSource cts;
		IPAddress localhostIP = IPAddress.Parse("127.0.0.1");
		public MainWindow()
		{
			InitializeComponent();
			viewModel = (MainWindowViewModel)DataContext;
		}
		private async void StartButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
			openFileDialog.InitialDirectory = Environment.CurrentDirectory;
			openFileDialog.Title = "Select config file";
			if (openFileDialog.ShowDialog() != true) return;
			
			//check if file exists
			if (!System.IO.File.Exists(openFileDialog.FileName))
			{
				errorHandler.AddError("StartButton_Click - >File does not exist!");
				return;
			}
			viewModel.SelectedConfigFilePath =openFileDialog.FileName;

			cts = new CancellationTokenSource();

			List<ConnectionInfo> configs = ConfigManager.LoadConfig(openFileDialog.FileName);
			List<Task> tasks = new List<Task>();

			foreach (var config in configs)
			{
				// Start proxy logic asynchronously for each configuration
				var task = StartProxyAsync(config, cts.Token);
				tasks.Add(task);
			}

			StartButton.IsEnabled = false;
			StopButton.IsEnabled = true;
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			cts.Cancel();
			StartButton.IsEnabled = true;
			StopButton.IsEnabled = false;
		}
		private void RefreshConfigButton_Click(object sender, RoutedEventArgs e)
		{
			// Load new configurations
			List<ConnectionInfo> configs = ConfigManager.LoadConfig(viewModel.SelectedConfigFilePath);
			if (configs.Count == 0) return;

			// Stop existing connections
			cts.Cancel();

			// Initialize CancellationTokenSource
			cts = new CancellationTokenSource();

			// Initialize TCP connections based on new configurations
			foreach (var config in configs)
			{
				// Start proxy logic asynchronously for each configuration
				_ = StartProxyAsync(config, cts.Token);
			}
		}

		private async Task StartProxyAsync(ConnectionInfo config, CancellationToken globalCancellationToken)
		{
			TcpListener listener = new TcpListener(localhostIP, config.ListeningPort);
			listener.Start();

			try
			{
				while (!globalCancellationToken.IsCancellationRequested)
				{
					// Accept incoming client
					TcpClient incomingClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
					incomingClient.Client.NoDelay = true;

					// Start the forwarding logic in a separate task
					var task = HandleClientAsync(incomingClient, config, globalCancellationToken);
				}
			}
			catch (Exception ex)
			{
				errorHandler.AddException(ex, "StartProxyAsync");
			}
			finally
			{
				listener.Stop();
			}
		}

		private async Task HandleClientAsync(TcpClient incomingClient, ConnectionInfo config, CancellationToken cancellationToken)
		{
			TcpClient outgoingClient = null;

			try
			{
				Dispatcher.Invoke(() => viewModel.ActiveConnections.Add(config));

				// Initialize and connect outgoing client
				IPEndPoint localEndPoint = new IPEndPoint(config.ViaIP, 0);
				outgoingClient = new TcpClient(localEndPoint);
				await outgoingClient.ConnectAsync(config.DestinationAddress.Address, config.DestinationAddress.Port);
				outgoingClient.Client.NoDelay = true;

				NetworkStream incomingStream = incomingClient.GetStream();
				NetworkStream outgoingStream = outgoingClient.GetStream();

				// Start forwarding streams
				Task forwardToOutgoing = ForwardStreamAsync(incomingStream, outgoingStream, cancellationToken);
				Task forwardToIncoming = ForwardStreamAsync(outgoingStream, incomingStream, cancellationToken);

				await Task.WhenAny(forwardToOutgoing, forwardToIncoming);
			}
			catch (Exception ex)
			{
				errorHandler.AddException(ex, "HandleClientAsync");
			}
			finally
			{
				incomingClient?.Close();
				incomingClient?.Dispose();
				outgoingClient?.Close();
				outgoingClient?.Dispose();
				Dispatcher.Invoke(() => viewModel.ActiveConnections.Remove(config));
			}
		}

		private async Task ForwardStreamAsync(NetworkStream incomingStream, NetworkStream outgoingStream, CancellationToken cancellationToken)
		{
			byte[] buffer = new byte[1024];
			int bytesRead;

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					bytesRead = await incomingStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

					if (bytesRead == 0)
					{
						cancellationToken.ThrowIfCancellationRequested();
						throw new IOException("Connection closed.");
					}
					await outgoingStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
				}
				catch (Exception ex)
				{
					errorHandler.AddException(ex, "ForwardStreamAsync");
					throw; 
				}
			}
			var torold = 1;
		}
	}
}
