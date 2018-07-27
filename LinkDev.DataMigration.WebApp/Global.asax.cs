using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.ModelBinding;
using System.Web.Routing;
using LinkDev.DataMigration.WebApp.Controllers;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using WebApi.OutputCache.Core.Cache;
using WebApi.OutputCache.V2;

namespace LinkDev.DataMigration.WebApp
{
	public class WebApiApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			GlobalConfiguration.Configuration.CacheOutputConfiguration()
				.RegisterCacheOutputProvider(() => new MemoryCacheDefault());
			GlobalConfiguration.Configuration.CacheOutputConfiguration()
				.RegisterDefaultCacheKeyGeneratorProvider(() => new DefaultCacheKeyGenerator());

			UnityConfig.RegisterTypes(UnityConfig.Container);

			GlobalConfiguration.Configure(WebApiConfig.Register);
		}
	}
}
