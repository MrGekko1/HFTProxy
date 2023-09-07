using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Windows;

namespace HFTProxy
{
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
}
