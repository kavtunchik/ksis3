using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace P2P_chat
{
	class User
	{
		public TcpClient chatUser;
		private int port;
		public string userName;
		public IPAddress userIP;
		public NetworkStream Stream;
		public bool isOnline;

		public bool IsOnline
		{
			get
			{
				Thread.Sleep(100);
				return isOnline;
			}
			set
			{
				isOnline = value;
			}
		}

		public User(TcpClient chatUser, int port)
		{
			this.chatUser = chatUser;
			this.port = port;
			userIP = ((IPEndPoint)chatUser.Client.RemoteEndPoint).Address;
			Stream = chatUser.GetStream();
		}

		public User(string userName, IPAddress userIP, int port)
		{
			this.userName = userName;
			this.userIP = userIP;
			this.port = port;
		}

		public void Connect()
		{
			IPEndPoint IPEndPoint = new IPEndPoint(userIP, port);
			chatUser = new TcpClient();
			chatUser.Connect(IPEndPoint);
			Stream = chatUser.GetStream();
		}

		public void SendMessage(Message TCPMessage)
		{
			byte[] message = Encoding.UTF8.GetBytes(TCPMessage.Code + TCPMessage.Text);
			Stream.Write(message, 0, message.Length);
		}

		public byte[] ReceiveMessage()
		{
			StringBuilder message = new StringBuilder();
			byte[] buffer = new byte[256];
			do
			{
				int size = Stream.Read(buffer, 0, buffer.Length);
				message.Append(Encoding.UTF8.GetString(buffer, 0, size));
			}
			while (Stream.DataAvailable);

			return Encoding.UTF8.GetBytes(message.ToString());
		}

		public void Close()
		{
			IsOnline = false;
			Stream.Close();
			chatUser.Close();
		}
	}
}