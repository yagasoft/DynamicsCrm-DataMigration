using System;
using System.Web.Http;
using LinkDev.DataMigration.WebApp.BLL;
using Unity;
using Unity.Lifetime;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.DataMigration.WebApp.SignalR;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Helpers;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Xrm.Sdk;
using Unity.Injection;

namespace LinkDev.DataMigration.WebApp
{
    /// <summary>
    /// Specifies the Unity configuration for the main container.
    /// </summary>
    public static class UnityConfig
    {
        #region Unity Container
        private static Lazy<IUnityContainer> container =
          new Lazy<IUnityContainer>(() =>
          {
              var container = new UnityContainer();
              RegisterTypes(container);
              return container;
          });

        /// <summary>
        /// Configured Unity Container.
        /// </summary>
        public static IUnityContainer Container => container.Value;
        #endregion

        /// <summary>
        /// Registers the type mappings with the Unity container.
        /// </summary>
        /// <param name="container">The unity container to configure.</param>
        /// <remarks>
        /// There is no need to register concrete types such as controllers or
        /// API controllers (unless you want to change the defaults), as Unity
        /// allows resolving a concrete type even if it was not previously
        /// registered.
        /// </remarks>
        public static void RegisterTypes(IUnityContainer container)
        {
			// NOTE: To load from web.config uncomment the line below.
			// Make sure to add a Unity.Configuration to the using statements.
			// container.LoadConfiguration();

	        container.RegisterType<CrmLog>(
		        new InjectionFactory(
			        c =>
					{
						var crmLog = new CrmLog("C:\\Logs\\LinkDev.DataMigration.csv", LogLevel.Debug,
							new FileConfiguration
							{
								FileDateFormat = "yyyy-MM-dd_HH-mm-ss-fff",
								FileSplitMode = SplitMode.Size,
								MaxFileSize = 10000
							}, "");
						crmLog.LogEntryAdded += (sender, args) => ProgressHub.PublishLog(
							args.LogEntry.Message, args.LogEntry.StartDate?.ToLocalTime() ?? DateTime.Now,
							args.LogEntry.Level, args.LogEntry.Information);
						return crmLog;
					}));
			container.RegisterType<IEnhancedOrgService>(new InjectionFactory(c => CrmService.GetService()));
        }
	}
}
