using Microsoft.Win32;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Serilog;

namespace HFTProxy
{


	public partial class MainWindow : Window
	{
		readonly ErrorHandler errorHandler = new();
		private readonly MainWindowViewModel viewModel;
		readonly ProxyManager? proxyManager;

		public MainWindow()
		{
			InitializeComponent();
			
			viewModel = (MainWindowViewModel)DataContext;
			errorHandler.OnNewError += ErrorHandler_OnNewError;
			proxyManager = new ProxyManager(errorHandler);
			proxyManager.ProxyStarted += ProxyManager_ProxyStarted;
			proxyManager.ProxyStopped += ProxyManager_ProxyStopped;
		}

		private void ErrorHandler_OnNewError(object? sender, (string message, string func) data)
		{
			Dispatcher.Invoke(() =>
			{
				TextBlockErrorCount.Text = $"Uncleared Errors: {errorHandler.UnclearedErrorCount}";
				Log.Error($"{data.func} - {data.message}");
			});
		}

		private void ProxyManager_ProxyStopped(object? sender, ConnectionInfo e) =>Dispatcher.Invoke(()=> this.viewModel.ActiveConnections.Remove(e));

		private void ProxyManager_ProxyStarted(object? sender, ConnectionInfo e) => Dispatcher.Invoke(() => this.viewModel.ActiveConnections.Add(e));

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new()
			{
				Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
				InitialDirectory = Environment.CurrentDirectory,
				Title = "Select config file"
			};
			if (openFileDialog.ShowDialog() != true) return;
			
			//check if file exists
			if (!System.IO.File.Exists(openFileDialog.FileName))
			{
				errorHandler.AddError("StartButton_Click - >File does not exist!", "StartButton_Click");
				return;
			}
			viewModel.SelectedConfigFilePath =openFileDialog.FileName;
			Log.Information($"StartButton_Click - Selected config file: {viewModel.SelectedConfigFilePath}");
			if(proxyManager is null)
			{
				errorHandler.AddError("StartButton_Click - >proxyManager is null!", "StartButton_Click");
				return;
			}

			bool result=((ProxyManager)proxyManager).LoadConfig(openFileDialog.FileName);

			if(result==false){return;}

			StartButton.IsEnabled = false;
			StopButton.IsEnabled = true;
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			Log.Information("StopButton_Click");
			proxyManager?.StopProxy();
			StartButton.IsEnabled = true;
			StopButton.IsEnabled = false;
		}

		private void RefreshConfigButton_Click(object sender, RoutedEventArgs e)
		{
			Log.Information("RefreshConfigButton_Click");
			proxyManager?.RefreshConfig();

			//// Load new configurations
			//List<ConnectionInfo> configs = ConfigManager.LoadConfig(viewModel.SelectedConfigFilePath);
			//if (configs.Count == 0) return;

			//// Stop existing connections
			//cts.Cancel();

			//// Initialize CancellationTokenSource
			//cts = new CancellationTokenSource();

			//// Initialize TCP connections based on new configurations
			//foreach (var config in configs)
			//{
			//	// Start proxy logic asynchronously for each configuration
			//	_ = StartProxyAsync(config, cts.Token);
			//}
		}

		private void ButtonClearErrors_Click(object sender, RoutedEventArgs e)
		{
			errorHandler.ClearErrors();
			TextBlockErrorCount.Text = "Uncleared Errors: 0";
		}
		private void ButtonOpenLogFile_Click(object sender, RoutedEventArgs e)
		{
			Log.Information("ButtonOpenLogFile_Click");
			string logDirectory = "logs";
			if(Directory.Exists(logDirectory)==false){ return;}

			var logFiles = Directory.GetFiles(logDirectory, "HFTProxy*.txt");
			string? mostRecentLogFile = logFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();

			if (File.Exists(mostRecentLogFile))
			{
				try
				{
					ProcessStartInfo psi = new()
					{
						FileName = mostRecentLogFile,
						UseShellExecute = true
					};
					Process.Start(psi);
				}
				catch (Exception ex)
				{
					Log.Error($"ButtonOpenLogFile_Click - An error occurred while opening the log file: {ex.Message}");
					MessageBox.Show($"An error occurred while opening the log file: {ex.Message}");
				}
			}
			else
			{
				Log.Error($"ButtonOpenLogFile_Click - Log file does not exist.");
				MessageBox.Show("Log file does not exist.");
			}
		}
	}
}
