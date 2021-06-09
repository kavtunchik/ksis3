using System;
using System.Windows;
using System.Net;
using System.Net.Sockets;

namespace P2P_chat
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private Connection userConnection;
		private IPAddress userIP;
		private IPAddress[] host;

		public MainWindow()
		{
			InitializeComponent();
			btSend.IsEnabled = false;
			tbMessage.IsEnabled = false;
			btDisconnect.IsEnabled = false;

			host = Array.FindAll(Dns.GetHostEntry(string.Empty).AddressList, address =>
												  address.AddressFamily == AddressFamily.InterNetwork);
			AddIPAddress();
			userIP = host[cbIPAddress.SelectedIndex];

			Closed += MainWindow_Closed;
		}

		private void MainWindow_Closed(object sender, EventArgs e)
		{
			userConnection?.Disconnect();
		}

		private void btConnect_Click(object sender, RoutedEventArgs e)
		{
			string userName = tbUserName.Text;
			userConnection = new Connection(userName, userIP, GetMessage, AddMessage, ClearScreen);
			userConnection.Connect();

			tbUserName.IsReadOnly = true;
			tbMessage.IsEnabled = true;
			btConnect.IsEnabled = false;
			btDisconnect.IsEnabled = true;
			btSend.IsEnabled = true;
		}
		private void ClearScreen()
		{
			//tbChat.Text = "";
		}

		private string GetMessage()
		{
			return tbMessage.Text;
		}

		private void AddMessage(string message)
		{
			tbChat.AppendText(message);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			userConnection.Disconnect();
			Environment.Exit(0);
		}

		private void btDisconnect_Click(object sender, RoutedEventArgs e)
		{
			userConnection.Disconnect();
			Environment.Exit(0);
		}

		private void btSend_Click(object sender, RoutedEventArgs e)
		{
			userConnection.SendMessage();
			tbMessage.Clear();
		}

		private void AddIPAddress()
		{
			foreach (IPAddress IP in host)
				cbIPAddress.Items.Add(IP);
			cbIPAddress.SelectedIndex = cbIPAddress.Items.Count - 1;
		}
	}
}