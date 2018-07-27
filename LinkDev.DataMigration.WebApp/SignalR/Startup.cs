#region Imports

using LinkDev.DataMigration.WebApp.SignalR;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Owin;
using Microsoft.Owin.Cors;

#endregion

[assembly: OwinStartup(typeof(Startup))]

namespace LinkDev.DataMigration.WebApp.SignalR
{
	public class Startup
	{
		public void Configuration(IAppBuilder app)
		{
			app.UseCors(CorsOptions.AllowAll);
			var hubConfiguration = new HubConfiguration { EnableDetailedErrors = true };
			app.MapSignalR(hubConfiguration);
		}
	}
}
