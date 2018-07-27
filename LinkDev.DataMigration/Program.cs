using System;
using System.Diagnostics;
using Nancy;

namespace LinkDev.DataMigration
{
	class Program
	{
		static void Main(string[] args)
		{
			using (Microsoft.Owin.Hosting.WebApp.Start<OwinConfiguration>("http://localhost:58001"))
			{
				StaticConfiguration.DisableErrorTraces = false;
				Console.WriteLine("Server running at http://localhost:58001.");
				Process.Start("http://localhost:58001");
				Console.WriteLine("");
				Console.WriteLine("Press any key to shutdown server ...");
				Console.ReadKey();
			}
		}
	}
}
