using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Helpers;
using LinkDev.Libraries.EnhancedOrgService.Pools;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using WebApi.OutputCache.Core.Cache;
using WebApi.OutputCache.V2;

namespace LinkDev.DataMigration.WebApp.Helpers
{
    public static class CrmService
	{
		internal static int Threads = 1;
		internal static string OrgId;
		private static string connectionString;
	    private static IEnhancedServicePool<EnhancedOrgService> ServicePool;

	    internal static string ConnectionString
	    {
		    get => connectionString;
		    set
		    {
			    if (value == connectionString)
			    {
				    return;
			    }

			    var tempPool = ServicePool;
			    var tempConnString = connectionString;
				connectionString = value;
			    CrmServiceClient connection = null;

				// test connection
				try
				{
					connection = new CrmServiceClient(connectionString);
					connection.Execute(new WhoAmIRequest());
					ServicePool = EnhancedServiceHelper.GetPool(connectionString);

					var urlSplit = connectionString.ToLower().Split(';').FirstOrDefault(e => e.Contains("url"))?.Split('=');
					OrgId = urlSplit?.Length > 1 ? urlSplit[1] : DateTime.Now.ToString();

					MemoryCache.Default.Trim(100);
				}
				catch
				{
					ServicePool = tempPool;
					connectionString = tempConnString;

					var exceptionType = connection?.LastCrmException?.GetType();

					if (exceptionType != null && typeof(Exception).IsAssignableFrom(exceptionType))
					{
						throw (Exception)Activator.CreateInstance(connection.LastCrmException.GetType(), $"{connection.LastCrmError} | {connection.LastCrmException.Message}");
					}

					throw;
				}
		    }
	    }

	    public static IEnhancedOrgService GetService()
		{
			ServicePool.Require(nameof(ServicePool));
			return ServicePool.GetService(Threads);
		}
	}
}
