using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using LinkDev.DataMigration.WebApp.BLL;
using LinkDev.DataMigration.WebApp.Models.Export;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Model;

namespace LinkDev.DataMigration.WebApp.Controllers
{
	[Log]
	[LogActionFilter]
	public class ExportController : ApiController
	{
		private readonly IEnhancedOrgService service;
		private readonly CrmLog log;

		private readonly CrmExporter crmExporter;

		public ExportController(IEnhancedOrgService service, CrmLog log)
		{
			this.service = service;
			this.log = log;
			crmExporter = new CrmExporter(service, log, new MigrationMetadataHelper(service, log));
		}

		// POST api/<controller>
		public async Task<IHttpActionResult> Post()
		{
			log.Log($"Exporting data using Post ...");

			var configuration = await Request.Content.ReadAsStringAsync();
			log.Log(new LogEntry($"'Post' body.", information: configuration));

			if (string.IsNullOrEmpty(configuration))
			{
				return BadRequest("Configuration cannot be empty.");
			}

			log.Log($"Converting from JSON to object ...");
			var jsonObject = ExportConfiguration.FromJson(configuration);
			log.Log($"Finished creating JSON object.");

			log.Log($"Exporting ...");
			var data = crmExporter.ExportRecords(jsonObject);
			log.Log($"Finished exporting data.");
			
			return Ok(data);
		}
	}
}
