using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Web.Http;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.DataMigration.WebApp.Models.Parameters;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using WebApi.OutputCache.V2;

namespace LinkDev.DataMigration.WebApp.Controllers
{
	public class CrmServiceController : ApiController
	{
		public IHttpActionResult Get()
		{
			return Ok(CrmService.ConnectionString);
		}

		// POST api/<controller>
		public IHttpActionResult Post(PostCrmServiceRequest request)
		{
			request.Require(nameof(request));
			request.ConnectionString.RequireNotEmpty(nameof(request.ConnectionString));
			request.MaxThreadCount?.RequireAbove(0, nameof(request.MaxThreadCount));

			CrmService.Threads = request.MaxThreadCount ?? 1;
			CrmService.ConnectionString = request.ConnectionString;

			return CrmService.ConnectionString == request.ConnectionString
				? (IHttpActionResult)Ok(CrmService.ConnectionString)
				: InternalServerError();
		}
	}
}
