using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

	/// 
	/// TODO:
	/// Some kind of a graph that shows the amount of data transferred per second should be added somewhere to see which connections are the most active and which is dead.
	/// If possible the average latency from the PC to server should be somehow measured and displayed, but without affecting the performance.
	/// Config file shold be validated before loading. In case of errors, the config file should not be loaded and error message should be displayed in a message box.
	/// </summary>
	/// 

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

			foreach (var line in lines)
			{
				if (line.Trim().StartsWith("#")) continue;  // Skip comments

				string[] parts = line.Split(',');
				if (parts.Length <= 3)
				{
					errors.Add($"Invalid configuration line: {line}");
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
		private readonly MainWindowViewModel viewModel;
		private CancellationTokenSource cts;
		//public ObservableCollection<ConnectionInfo> ActiveConnections { get; set; }
		IPAddress localhostIP = IPAddress.Parse("127.0.0.1");
		public MainWindow()
		{
			InitializeComponent();
			viewModel = (MainWindowViewModel)DataContext;
		}

		private async void ShowMessageBoxAsync(string message) => await Task.Run(() => MessageBox.Show(message));

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
				ShowMessageBoxAsync("File does not exist!");
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

		private void CloseAndDisposeClients(TcpClient incomingClient, TcpClient outgoingClient, TcpListener listener)
		{
			incomingClient?.Close();
			incomingClient?.Dispose();

			outgoingClient?.Close();
			outgoingClient?.Dispose();

			//listener?.Stop();
		}

		private async Task StartProxyAsync(ConnectionInfo config, CancellationToken globalCancellationToken)
		{
			TcpListener listener = new TcpListener(localhostIP, config.ListeningPort);
			listener.Start();

			using (CancellationTokenSource localCts = new CancellationTokenSource())
			{
				CancellationToken linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCancellationToken, localCts.Token).Token;

				try
				{
					while (!linkedCts.IsCancellationRequested)
					{
						TcpClient incomingClient = null;
						TcpClient outgoingClient = null;

						try
						{
							incomingClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
							incomingClient.Client.NoDelay = true;

							NetworkStream incomingStream = incomingClient.GetStream();

							IPEndPoint localEndPoint = new IPEndPoint(config.ViaIP, 0);
							outgoingClient = new TcpClient(localEndPoint);

							Dispatcher.Invoke(() => viewModel.ActiveConnections.Add(config));

							await outgoingClient.ConnectAsync(config.DestinationAddress.Address, config.DestinationAddress.Port).ConfigureAwait(false);
							outgoingClient.Client.NoDelay = true;

							NetworkStream outgoingStream = outgoingClient.GetStream();

							Task task1 = ForwardStreamAsync(incomingStream, outgoingStream, linkedCts);
							Task task2 = ForwardStreamAsync(outgoingStream, incomingStream, linkedCts);

							await Task.WhenAny(task1, task2).ConfigureAwait(false);

							if (task1.IsFaulted) throw task1.Exception;
							if (task2.IsFaulted) throw task2.Exception;
						}
						catch (SocketException se)
						{
							ShowMessageBoxAsync($"Socket Exception: {se.Message}");
						}
						catch (Exception ex)
						{
							localCts.Cancel();
							ShowMessageBoxAsync($"General Exception: {ex.Message}");
						}
						finally
						{
							CloseAndDisposeClients(incomingClient, outgoingClient, null);
							Dispatcher.Invoke(() => viewModel.ActiveConnections.Remove(config));
						}
					}
				}
				catch (OperationCanceledException oce)
				{
					ShowMessageBoxAsync(oce.Message);
				}
				finally
				{
					CloseAndDisposeClients(null, null, listener);
					Dispatcher.Invoke(() => viewModel.ActiveConnections.Remove(config));
				}
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
				catch (IOException ex)
				{
					ShowMessageBoxAsync($"cancellationToken.IsCancellationRequested:{cancellationToken.IsCancellationRequested}\n IOException in ForwardStreamAsync: {ex.Message}");
					throw; 
				}
				catch (Exception ex)
				{
					ShowMessageBoxAsync($"General Exception in ForwardStreamAsync: {ex.Message}");
					throw;
				}
			}
		}
	}
}
