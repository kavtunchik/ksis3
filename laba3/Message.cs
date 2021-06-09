using System.Text;

namespace P2P_chat
{
	public enum TMessage { Connect, Message, SendHistory, ShowHistory, ExitUser };
	class Message
	{
		public int Code { get; protected set; }
		public string Text { get; protected set; }

		public Message(int code, string text)
		{
			Code = code;
			Text = text;
		}

		public Message(byte[] message)
		{
			string stringMessage = Encoding.UTF8.GetString(message);
			Code = int.Parse(stringMessage[0].ToString());
			Text = stringMessage.Substring(1);
		}
	}
}