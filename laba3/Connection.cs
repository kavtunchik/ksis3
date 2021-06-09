using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;

namespace P2P_chat
{
	delegate string GetMessage();
	delegate void AddMessage(string message);
	delegate void ClearScreen();

	class Connection
	{
		public const int sendPort = 50000;
		public const int receivePort = 50001;
		public const int tcpPort = 50002;

		private GetMessage GetUserMessage;
		private AddMessage AddUserMessage;
		private ClearScreen ClearUserScreen;

		public static object locker = new object();

		private string userName;
		private IPAddress userIP;

		private static List<User> Users = new List<User>();
		private StringBuilder history;
		private SynchronizationContext synchronizationContext;

		private bool udpListen;
		private bool tcpListen;

		public Connection(string user, IPAddress userip, GetMessage getDel, AddMessage addDel, ClearScreen clrDel)
		{
			userName = user;
			userIP = userip;
			GetUserMessage = getDel;
			AddUserMessage = addDel;
			history = new StringBuilder();
			synchronizationContext = SynchronizationContext.Current;
			ClearUserScreen = clrDel;
		}

		public void Connect()
		{
			try
			{
				IPAddress BroadcastIP = IPAddress.Parse(userIP.ToString().Substring(
										0, userIP.ToString().LastIndexOf('.') + 1) + "255");

				SendBroadcastPackage(BroadcastIP);

				Task.Run(ListenToUDP);
				Task.Run(ListenToTCP);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
				Disconnect();
			}
		}

		public void Disconnect()
		{
			string message = $"{userName} [{userIP}] ({DateTime.Now}) disconnected.\n";
			AddUserMessage(message);
			history.Append(message);
			Message tcpMessage = new Message((int)TMessage.ExitUser, message);

			udpListen = false;
			tcpListen = false;

			foreach (User user in Users)
			{
				try
				{
					user.SendMessage(tcpMessage);
				}
				catch
				{
					MessageBox.Show($"Can't send the message to {user.userName} about disconnecting.",
						"Error!", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					user.Close();
				}
			}

			Users.Clear();
		}

		public void SendBroadcastPackage(IPAddress BroadcastIP)
		{
			IPEndPoint sourceEP = new IPEndPoint(userIP, sendPort); // от кого 
			IPEndPoint destinationEP = new IPEndPoint(BroadcastIP, receivePort); // кому (всем)

			UdpClient udpSender = new UdpClient(sourceEP);
			udpSender.EnableBroadcast = true;

			byte[] messageBytes = Encoding.UTF8.GetBytes(userName);

			try
			{
				int count = udpSender.Send(messageBytes, messageBytes.Length, destinationEP);

				AddUserMessage($"You [{userIP}] ({DateTime.Now}) joined the chat.\n");
				history.Append($"{userName} [{userIP}] ({DateTime.Now}) joined the chat.\n");
			}
			catch (Exception e)
			{
				MessageBox.Show(e.ToString());
				MessageBox.Show("Can't send the information about the new user.", "Error!",
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				udpSender.Close();
			}
		}

		private void ListenToUDP()
		{
			IPEndPoint localEP = new IPEndPoint(userIP, receivePort);
			IPEndPoint remoteEP = new IPEndPoint(IPAddress.Broadcast, sendPort);

			UdpClient udpReceiver = new UdpClient(localEP);

			udpListen = true;
			while (udpListen)
			{
				byte[] message = udpReceiver.Receive(ref remoteEP);
				string userName = Encoding.UTF8.GetString(message);

				User newUser = new User(userName, remoteEP.Address, tcpPort);

				if (Users.Find(x => x.userIP.ToString() == remoteEP.Address.ToString()) == null &&
							   userIP.ToString() != remoteEP.Address.ToString())
				{
					newUser.Connect();

					Message tcpMessage = new Message((int)TMessage.Connect, this.userName);
					newUser.SendMessage(tcpMessage);

					lock (locker)
					{
						Users.Add(newUser);
					}

					Task.Run(() => ListenUser(newUser));

					synchronizationContext.Post(
						delegate
						{
							AddUserMessage($"{newUser.userName} [{newUser.userIP}] ({DateTime.Now}) joined the chat.\n");
							history.Append($"{newUser.userName} [{newUser.userIP}] ({DateTime.Now}) joined the chat.\n");
						}, null);
				}
			}
			udpReceiver.Close();
		}

		private void ListenToTCP()
		{
			TcpListener tcpListener = new TcpListener(userIP, tcpPort);
			tcpListener.Start();

			tcpListen = true;
			while (tcpListen)
			{
				TcpClient tcpClient = tcpListener.AcceptTcpClient();
				User newUser = new User(tcpClient, tcpPort);

				if (Users.Find(x => x.userIP.ToString() == newUser.userIP.ToString()) == null &&
							   userIP.ToString() != newUser.userIP.ToString())
				{
					lock (locker)
					{
						Users.Add(newUser);
					}

					Task.Run(() => ListenUser(newUser));
				}
			}

			tcpListener.Stop();
		}

		private void ListenUser(User user)
		{
			while (user.IsOnline)
			{
				if (user.Stream.DataAvailable) // доступны ли для чтения данные
				{
					byte[] message = user.ReceiveMessage();
					Message tcpMessage = new Message(message);

					switch ((TMessage)tcpMessage.Code)
					{
						case TMessage.Connect:
							user.userName = tcpMessage.Text;
							GetHistory((int)TMessage.SendHistory, user);
							break;

						case TMessage.Message:
							synchronizationContext.Post(delegate
							{
								string messageChat =
									$"{user.userName} [{user.userIP}] ({DateTime.Now}): {tcpMessage.Text}\n";
								AddUserMessage(messageChat);
								history.Append(messageChat);
							}, null);
							break;

						case TMessage.SendHistory:
							GetHistory((int)TMessage.ShowHistory, user);
							break;

						case TMessage.ShowHistory:
							synchronizationContext.Post(delegate
							{
								ClearUserScreen();
								HistoryPreparing(AddUserMessage, tcpMessage.Text);
								history.Remove(0, history.Length);
								history.Append(tcpMessage.Text);
							}, null);
							break;

						case TMessage.ExitUser:
							user.Close();
							Users.Remove(user);

							synchronizationContext.Post(delegate
							{
								string messageChat = $"{tcpMessage.Text}";
								history.Append(messageChat);
								AddUserMessage(messageChat);
							}, null);
							break;
					}
				}
			}
		}

		private void GetHistory(int code, User user)
		{
			string message = (code == (int)TMessage.SendHistory) ? string.Empty : history.ToString();
			Message tcpHistoryMessage = new Message(code, message);

			user.SendMessage(tcpHistoryMessage);
		}

		public void SendMessage()
		{
			string message = GetUserMessage();
			Message tcpMessage = new Message((int)TMessage.Message, message);

			foreach (User user in Users)
			{
				try
				{
					user.SendMessage(tcpMessage);
				}
				catch
				{
					MessageBox.Show($"Can't send the message to {user.userName}.",
						"Error!", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}

			AddUserMessage($"You ({DateTime.Now}): {message}\n");
			history.Append($"{userName} [{userIP}] ({DateTime.Now}): {message}\n");
		}

		public void HistoryPreparing(AddMessage addMessage, string text)
		{
			int historyCount = 0;
			string userHistory = "", history = text;
			string message;
			while (history != "")
			{
				message = history.Substring(0, history.IndexOf('\n') + 1);
				historyCount += history.IndexOf('\n');
				history = history.Remove(0, history.IndexOf('\n') + 1);

				if (message.Contains(userIP.ToString()))
				{
					message = message.Remove(0, message.IndexOf(userIP.ToString()) - 1);
					message = message.Insert(0, "You ");
				}
				userHistory += message;
			}
			addMessage(userHistory);
		}
	}
}