using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using System.Linq;

namespace HFTProxy
{
	public class ClientRequestCommandMessage
	{
		public string TargetIP { get; set; } = "";
		public int TargetPort { get; set; } = -1;
		public string ViaIP { get; set; } = "";
	}

	public class ServerResponseCommandMessage
	{
		public string ResponseType { get; set; } = "";
		public int Port { get; set; }=-1;
		public string Comment { get; set; } = "";
	}

	public class CommandListener
	{
		private readonly int listeningPort;
		readonly ProxyManager proxyManager;
		public CommandListener(ProxyManager proxyManager, int listeningPort)
		{
			this.proxyManager = proxyManager;
			this.listeningPort = listeningPort;
		}

		public async Task StartListeningAsync()
		{
			TcpListener listener = new(IPAddress.Any, listeningPort);
			listener.Start();

			Console.WriteLine($"HFTProxy is listening on port {listeningPort}");

			while (true)
			{
				var client = await listener.AcceptTcpClientAsync();
				Console.WriteLine("Client connected");

				// Válasz a kliensnek
				await HandleClientAsync(client);
				client.Close();
			}
		}

		private async Task HandleClientAsync(TcpClient client)
		{
			using var stream = client.GetStream();
			using var reader = new StreamReader(stream, Encoding.ASCII);
			using var writer = new StreamWriter(stream, Encoding.ASCII);

			try
			{
				var clientMessage = await reader.ReadLineAsync();
				if (string.IsNullOrWhiteSpace(clientMessage))
				{
					throw new Exception("Empty request from client.");
				}

				// Most feltételezve, hogy a kliensnek csak az IP-t és célportot kell elküldenie
				ClientRequestCommandMessage? clientRequest = JsonSerializer.Deserialize<ClientRequestCommandMessage>(clientMessage) ?? throw new Exception("Invalid request from client.");
				bool result = proxyManager.TryGetPort(clientRequest.TargetIP, clientRequest.TargetPort, clientRequest.ViaIP, out int port);
				ServerResponseCommandMessage response;
				if (result == true)
				{
					response = new ServerResponseCommandMessage
					{
						ResponseType = "Success",
						Port = 8088,
						Comment = "Your request has been processed."
					};
				}
				else 
				{
					response = new ServerResponseCommandMessage
					{
						ResponseType = "Error",
						Port = -1,
						Comment = "Could not get port for you."
					};
				}

				var jsonResponse = JsonSerializer.Serialize(response);
				await writer.WriteLineAsync(jsonResponse);
				await writer.FlushAsync();
			}
			catch (Exception e)
			{
				var errorResponse = new ServerResponseCommandMessage
				{
					ResponseType = "Error",
					Port = 0,
					Comment = $"Error processing request: {e.Message}"
				};

				var jsonResponse = JsonSerializer.Serialize(errorResponse);
				await writer.WriteLineAsync(jsonResponse);
				await writer.FlushAsync();
			}
		}
	}


	public class ProxyManager
	{
		private readonly List<Task> proxyTasks;
		private readonly CancellationTokenSource cts=new();
		private readonly ErrorHandler errorHandler;
		readonly IPAddress localhostIP = IPAddress.Parse("127.0.0.1");
		readonly ConfigManager configManager = new();
		public event EventHandler<ConnectionInfo>? ProxyStarted;
		public event EventHandler<ConnectionInfo>? ProxyStopped;
		string lastLoadedFile=string.Empty;
		readonly int CommandPort = 8001;
		readonly List<ConnectionInfo> loadedConnections= new();
		public ProxyManager(ErrorHandler errorHandler)
		{
			this.errorHandler = errorHandler;
			proxyTasks = new List<Task>();

			//checks if the command port is available
			if (IsPortAvailable(localhostIP.ToString(), CommandPort) == false)
			{
				MessageBox.Show("Port 8001 is not available. Exiting.");
				Environment.Exit(0);
			}
		}

		public bool TryGetPort(string TargetIP, int targetPort,string viaIP, out int newListeningPort)
		{
			var ports= loadedConnections.Select(x=>x.ListeningPort).ToList();
			newListeningPort = 8081;

			if(IPAddress.TryParse(viaIP, out IPAddress? viaIPAddress) == false)
			{
				newListeningPort = -1;
				return false;
			}	

			if(IPEndPoint.TryParse(TargetIP+":"+ targetPort, out IPEndPoint? destinationAddress) == false)
			{
				newListeningPort = -1;
				return false;
			}

			while(true)
			{
				if (ports.Contains(newListeningPort) == false)
				{
	
					ConnectionInfo connectionInfo = new(newListeningPort, destinationAddress, viaIPAddress, "Requested via command port");
					return true;
				}
				newListeningPort++;
			}
		}

		public void RefreshConfig()
		{
			MessageBox.Show("NotImplementedException");
			return;
		}

		public bool LoadConfig(string filePath)
		{
			var configs =this.configManager.LoadConfig(filePath);
			if (configs.Count == 0) 
			{
				return false;
			}
			lastLoadedFile = filePath;
			StartProxy(configs);
			return true;
		}

		public void StartProxy(List<ConnectionInfo> configs)
		{
			foreach (var config in configs)
			{
				// Start proxy logic asynchronously for each configuration
				var task = StartProxyAsync(config, cts.Token);
				proxyTasks.Add(task);
				loadedConnections.Add(config);
			}
		}

		public void StopProxy()
		{
			cts.Cancel();
			cts.Dispose();
			proxyTasks.Clear();
		}

		private async Task StartProxyAsync(ConnectionInfo config, CancellationToken globalCancellationToken)
		{
			Log.Information($"Starting proxy for confing:{config}");
			TcpListener listener = new(localhostIP, config.ListeningPort);
			listener.Start();

			try
			{
				while (!globalCancellationToken.IsCancellationRequested)
				{
					// Accept incoming client
					TcpClient incomingClient = await listener.AcceptTcpClientAsync(globalCancellationToken).ConfigureAwait(false);
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
			Log.Information($"Proxy stopped for confing:{config}");
		}

		private async Task HandleClientAsync(TcpClient incomingClient, ConnectionInfo config, CancellationToken cancellationToken)
		{
			TcpClient? outgoingClient = null;

			try
			{
				ProxyStarted?.Invoke(this, config);

				// Initialize and connect outgoing client
				IPEndPoint localEndPoint = new(config.ViaIP, 0);
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
				ProxyStopped?.Invoke(this, config);
			}
		}

		private async Task ForwardStreamAsync(NetworkStream incomingStream, NetworkStream outgoingStream, CancellationToken cancellationToken)
		{
			byte[] buffer = new byte[1024];
			int bytesRead;

			while (!cancellationToken.IsCancellationRequested)
			{
				cancellationToken.ThrowIfCancellationRequested(); // Check for cancellation
				try
				{
					bytesRead = await incomingStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

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
		}
		public static bool IsPortAvailable(string IP, int port)
		{
			try
			{
				TcpListener tcpListener = new(IPAddress.Parse(IP), port);
				tcpListener.Start();
				tcpListener.Stop();
				return true;
			}
			catch (SocketException)
			{
				return false;
			}
		}
	}


}
