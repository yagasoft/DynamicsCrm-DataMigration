#region Imports

using System;
using System.Threading;
using LinkDev.Libraries.Common;
using Microsoft.AspNet.SignalR;

#endregion

namespace LinkDev.DataMigration.WebApp.SignalR
{
	public class ProgressHub : Hub
	{
		private static readonly IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<ProgressHub>();
		private static readonly BlockingQueue<Message> messages = new BlockingQueue<Message>();
		private static Timer publishingLogThread;

		public void SubscribeToProgress()
		{
			Groups.Add(Context.ConnectionId, "progress");
		}

		public static void PublishProgress(string id, string message, int progress)
		{
			hubContext.Clients.Group("progress").publishProgress(id, message, progress);
		}

		public void UnsubscribeFromProgress()
		{
			Groups.Remove(Context.ConnectionId, "progress");
		}

		public void SubscribeToLog()
		{
			Groups.Add(Context.ConnectionId, "Log");
			publishingLogThread = publishingLogThread
				?? new Timer(state =>
							 {
								 var message = messages.Dequeue();
								 hubContext.Clients.Group("Log")
									 .publishLog(message.message, message.date, message.level, message.details);
							 }, null, 0, 50);
		}

		public static void PublishLog(string message, DateTime date, LogLevel level, string details = null)
		{
			if (level != LogLevel.Debug && level != LogLevel.None)
			{
				messages.Enqueue(new Message(message, date.ToString("yyyy-MMM-dd hh:mm:ss tt"), level.ToString(), details));
			}
		}

		public void UnsubscribeFromLog()
		{
			Groups.Remove(Context.ConnectionId, "Log");
		}

		private class Message
		{
			internal string message;
			internal string date;
			internal string level;
			internal string details;

			public Message(string message, string date, string level, string details)
			{
				this.message = message;
				this.date = date;
				this.level = level;
				this.details = details;
			}
		}
	}
}
