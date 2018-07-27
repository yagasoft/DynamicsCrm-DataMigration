using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Validation;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;

namespace LinkDev.DataMigration.WebApp
{
	public static class WebApiConfig
	{
		public static void Register(HttpConfiguration config)
		{
			BuildConfig(config);
		}

		public static HttpConfiguration BuildConfig(HttpConfiguration config)
		{
			// Web API configuration and services
			//var cors = new EnableCorsAttribute("*", "*", "*");
			//config.EnableCors(cors);

			// Web API routes
			config.MapHttpAttributeRoutes();

			config.Routes.MapHttpRoute(
				name: "DefaultApi",
				routeTemplate: "api/{controller}/{id}",
				defaults: new
						  {
							  id = RouteParameter.Optional
						  }
				);

			return config;
		}
	}
}
