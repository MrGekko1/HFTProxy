using System;
using Serilog;

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
	/// 
	/// My idea is to add a textblock that shows the number of uncleared errors. Then add an OpenLogFile button and a ClearErrors button. The logging should not affect the performance of the proxy. the ClearErrors button should set the number of uncleared errors to 0.
	/// </summary>
	/// 
	public class ErrorHandler 
	{
		public int UnclearedErrorCount { get; private set; }

		public event EventHandler<(string message,string func)>? OnNewError;
		public void AddException(Exception ex, string function)
		{
			Log.Error($"{function} - Exception:{ex.Message}");

			UnclearedErrorCount++;
			OnNewError?.Invoke(this, (ex.Message, function));
		}
		public void AddError(string message, string function) 
		{
			Log.Error($"{function} - Error:{message}");

			UnclearedErrorCount++;
			OnNewError?.Invoke(this, (message, function));
		}

		internal void ClearErrors() => UnclearedErrorCount = 0;
	}
}
