using System.Net;

namespace HFTProxy
{
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
		public override string ToString()
		{
			return $"/ConnectionInfo ListeningPort:{ListeningPort} DestinationAddress:{DestinationAddress} ViaIP:{ViaIP} Comment:{Comment}/";
		}
	}
}
