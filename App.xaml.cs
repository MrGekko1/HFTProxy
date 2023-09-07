using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HFTProxy
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			Log.Logger = new LoggerConfiguration()
				.WriteTo.Async(a => a.Console())
				.WriteTo.Async(a => a.File("logs/HFTProxy.txt", rollingInterval: RollingInterval.Day))
				.CreateLogger();

			Log.Information("The application is starting.");

			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			Log.Information("The application is exiting.");
			Log.CloseAndFlush();

			base.OnExit(e);
		}
	}
}
