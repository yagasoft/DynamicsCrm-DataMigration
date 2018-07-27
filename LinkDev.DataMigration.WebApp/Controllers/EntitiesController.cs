using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using LinkDev.DataMigration.WebApp.BLL;
using LinkDev.DataMigration.WebApp.SignalR;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.AspNet.SignalR;
using Microsoft.Xrm.Sdk;
using WebApi.OutputCache.V2;

namespace LinkDev.DataMigration.WebApp.Controllers
{
	[Log]
	[CacheOutput(ServerTimeSpan = 600)]
	[LogActionFilter]
	public class EntitiesController : ApiControllerBase
	{
		private readonly MigrationMetadataHelper migrationMetadataHelper;

		public EntitiesController(IEnhancedOrgService service, CrmLog log)
			: base(service, log)
		{
			migrationMetadataHelper = new MigrationMetadataHelper(service, log);
		}

		// GET api/entities
		public IHttpActionResult Get()
		{
			return Ok(migrationMetadataHelper.GetEntityNames());
		}

		[Route("api/entities/{logicalName}")]
		public IHttpActionResult Get(string logicalName)
		{
			if (string.IsNullOrEmpty(logicalName))
			{
				return BadRequest("Logical Name cannot be empty.");
			}

			return Ok(migrationMetadataHelper.GetEntityMetaData(logicalName));
		}
	}
}
