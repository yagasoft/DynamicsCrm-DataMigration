#region Imports

using System;
using System.Web.Http;
using LinkDev.DataMigration.WebApp;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.DataMigration.WebApp.SignalR;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Owin.Cors;
using Ninject;
using Ninject.Web.Common.OwinHost;
using Ninject.Web.WebApi.OwinHost;
using Owin;

#endregion

namespace LinkDev.DataMigration
{
	public class OwinConfiguration
	{
		public void Configuration(IAppBuilder app)
		{
			var config = new HttpConfiguration();
			app.UseWebApi(WebApiConfig.BuildConfig(config));
			app.Map("/signalr",
				map =>
				{
					map.UseCors(CorsOptions.AllowAll);
					map.RunSignalR();
				});
			app.UseNancy();
			app.UseNinjectMiddleware(CreateKernel).UseNinjectWebApi(config);
		}

		private static StandardKernel CreateKernel()
		{
			var kernel = new StandardKernel();
			kernel.Bind<IEnhancedOrgService>().ToMethod(context => CrmService.GetService());
			kernel.Bind<CrmLog>().ToMethod(
				context =>
				{
					var crmLog = new CrmLog(
						"C:\\Logs\\LinkDev.DataMigration.csv",
						LogLevel.Debug,
						new FileConfiguration
						{
							FileDateFormat = "yyyy-MM-dd_HH-mm-ss-fff",
							FileSplitMode = SplitMode.Size,
							MaxFileSize = 10000
						});
					crmLog.LogEntryAdded += (sender, args) => ProgressHub.PublishLog(
						args.LogEntry.Message, args.LogEntry.StartDate?.ToLocalTime() ?? DateTime.Now,
						args.LogEntry.Level, args.LogEntry.Information);
					return crmLog;
				});
			return kernel;
		}
	}
}
