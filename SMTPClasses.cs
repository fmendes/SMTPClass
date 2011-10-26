using System;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Web.Mail;

namespace SMTPClasses
{
	/// <summary>
	/// provides methods to send email via smtp direct to mail server
	/// </summary>
	public class DirectSMTP
	{
		/// <summary>
		/// Get / Set the name of the SMTP mail server
		/// </summary>
		public static string SmtpServer;

		public static string SenderEmailAddr = "dont-reply@emsc.net";
		public static string SmtpServerAddr = "rtiexvs.emsc.root01.org";

		private enum SMTPResponse : int
		{
			CONNECT_SUCCESS = 220,
			GENERIC_SUCCESS = 250,
			DATA_SUCCESS = 354,
			QUIT_SUCCESS = 221

		}


		public static String Send(StringBuilder strbBodyParam, String strSubjectParam,
			String strEmailAddrParam)
		{
			String strCurrentStatus = "";

			// prepare message with the report
			MailMessage mmEmailMsg = new MailMessage();
			mmEmailMsg.Body = strbBodyParam.ToString();
			mmEmailMsg.From = SenderEmailAddr;
			mmEmailMsg.Subject = strSubjectParam;
			mmEmailMsg.To = strEmailAddrParam;

			// send email with the report
			//			DirectSMTP.SmtpServer	= "10.10.10.30";
			// DirectSMTP.SmtpServer	= "smtpgtwy.emcare.com";
			// Changed SMTP server  - 6/21/07, grf
			//
			DirectSMTP.SmtpServer = SmtpServerAddr;
			String strResponse = DirectSMTP.Send(mmEmailMsg);
			if (!strResponse.Equals(""))
			{
				strCurrentStatus = String.Format(
					"ERROR: Could not send email to {0} through {1}\r\n\r\n{2}.",
					strEmailAddrParam, DirectSMTP.SmtpServer, strResponse);
			}
			else
			{
				strCurrentStatus = String.Format("Sent report to email {0}", strEmailAddrParam);
			}

			return strCurrentStatus;
		}


		public static String Send(MailMessage message)
		{
			String strResponse = "";
			IPHostEntry IPhst = Dns.GetHostEntry(SmtpServer);
			IPEndPoint endPt = new IPEndPoint(IPhst.AddressList[0], 25);
			Socket s = new Socket(endPt.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				s.Connect(endPt);
			}
			catch (System.Net.Sockets.SocketException excpt)
			{
				s.Close();
				return String.Format(strResponse + "\r\n{0}", excpt.Message);
			}

			strResponse = Check_Response(s, SMTPResponse.CONNECT_SUCCESS);
			if (!strResponse.Equals(""))
			{
				s.Close();
				return strResponse;
			}

			Senddata(s, string.Format("HELO {0}\r\n", Dns.GetHostName()));
			strResponse = Check_Response(s, SMTPResponse.GENERIC_SUCCESS);
			if (!strResponse.Equals(""))
			{
				s.Close();
				return strResponse;
			}

			Senddata(s, string.Format("MAIL From: {0}\r\n", message.From));
			strResponse = Check_Response(s, SMTPResponse.GENERIC_SUCCESS);
			if (!strResponse.Equals(""))
			{
				s.Close();
				return strResponse;
			}

			string _To = message.To;
			string[] Tos = _To.Split(new char[] { ';', ',' });
			foreach (string To in Tos)
			{
				Senddata(s, string.Format("RCPT TO: {0}\r\n", To));
				strResponse = Check_Response(s, SMTPResponse.GENERIC_SUCCESS);
				if (!strResponse.Equals(""))
				{
					s.Close();
					return strResponse;
				}
			}

			if (message.Cc != null)
			{
				Tos = message.Cc.Split(new char[] { ';', ',' });
				foreach (string To in Tos)
				{
					Senddata(s, string.Format("RCPT TO: {0}\r\n", To));
					strResponse = Check_Response(s, SMTPResponse.GENERIC_SUCCESS);
					if (!strResponse.Equals(""))
					{
						s.Close();
						return strResponse;
					}
				}
			}

			StringBuilder Header = new StringBuilder();
			Header.Append("From: " + message.From + "\r\n");
			Tos = message.To.Split(new char[] { ';', ',' });
			Header.Append("To: ");
			for (int i = 0; i < Tos.Length; i++)
			{
				Header.Append(i > 0 ? "," : "");
				Header.Append(Tos[i]);
			}
			Header.Append("\r\n");
			if (message.Cc != null)
			{
				Tos = message.Cc.Split(new char[] { ';' });
				Header.Append("Cc: ");
				for (int i = 0; i < Tos.Length; i++)
				{
					Header.Append(i > 0 ? "," : "");
					Header.Append(Tos[i]);
				}
				Header.Append("\r\n");
			}
			Header.Append("Date: ");
			Header.Append(DateTime.Now.ToString("ddd, d M y H:m:s z"));
			Header.Append("\r\n");
			Header.Append("Subject: " + message.Subject + "\r\n");
			Header.Append("X-Mailer: DirectSMTP v1\r\n");
			string MsgBody = message.Body;
			if (!MsgBody.EndsWith("\r\n"))
				MsgBody += "\r\n";
			if (message.Attachments.Count > 0)
			{
				Header.Append("MIME-Version: 1.0\r\n");
				Header.Append("Content-Type: multipart/mixed; boundary=unique-boundary-1\r\n");
				Header.Append("\r\n");
				Header.Append("This is a multi-part message in MIME format.\r\n");
				StringBuilder sb = new StringBuilder();
				sb.Append("--unique-boundary-1\r\n");
				sb.Append("Content-Type: text/plain\r\n");
				sb.Append("Content-Transfer-Encoding: 7Bit\r\n");
				sb.Append("\r\n");
				sb.Append(MsgBody + "\r\n");
				sb.Append("\r\n");

				foreach (object o in message.Attachments)
				{
					MailAttachment a = o as MailAttachment;
					byte[] binaryData;
					if (a != null)
					{
						FileInfo f = new FileInfo(a.Filename);
						sb.Append("--unique-boundary-1\r\n");
						sb.Append("Content-Type: application/octet-stream; file=" + f.Name + "\r\n");
						sb.Append("Content-Transfer-Encoding: base64\r\n");
						sb.Append("Content-Disposition: attachment; filename=" + f.Name + "\r\n");
						sb.Append("\r\n");
						FileStream fs = f.OpenRead();
						binaryData = new Byte[fs.Length];
						long bytesRead = fs.Read(binaryData, 0, (int)fs.Length);
						fs.Close();
						string base64String = System.Convert.ToBase64String(binaryData, 0, binaryData.Length);

						for (int i = 0; i < base64String.Length; )
						{
							int nextchunk = 100;
							if (base64String.Length - (i + nextchunk) < 0)
								nextchunk = base64String.Length - i;
							sb.Append(base64String.Substring(i, nextchunk));
							sb.Append("\r\n");
							i += nextchunk;

						}
						sb.Append("\r\n");

					}
				}

				MsgBody = sb.ToString();
			}

			Senddata(s, "DATA\r\n");
			strResponse = Check_Response(s, SMTPResponse.DATA_SUCCESS);
			if (!strResponse.Equals(""))
			{
				s.Close();
				return strResponse;
			}

			Header.Append("\r\n");
			Header.Append(MsgBody);
			Header.Append(".\r\n\r\n\r\n");
			Senddata(s, Header.ToString());
			strResponse = Check_Response(s, SMTPResponse.GENERIC_SUCCESS);
			if (!strResponse.Equals(""))
			{
				s.Close();
				return strResponse;
			}

			Senddata(s, "QUIT\r\n");
			strResponse = Check_Response(s, SMTPResponse.QUIT_SUCCESS);

			s.Close();
			return strResponse;
		}


		private static void Senddata(Socket s, string msg)
		{
			byte[] _msg = Encoding.ASCII.GetBytes(msg);
			s.Send(_msg, 0, _msg.Length, SocketFlags.None);
		}


		private static String Check_Response(Socket s, SMTPResponse eResponseExpectedParam)
		{
			string sResponse;
			int iResponse;
			byte[] bytes = new byte[1024];
			while (s.Available == 0)
				System.Threading.Thread.Sleep(100);

			s.Receive(bytes, 0, s.Available, SocketFlags.None);
			sResponse = Encoding.ASCII.GetString(bytes);
			iResponse = Convert.ToInt32(sResponse.Substring(0, 3));
			if (iResponse != (int)eResponseExpectedParam)
				return sResponse;

			return "";
		}


	}
}
