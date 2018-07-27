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

namespace LinkDev.DataMigration.WebApp.Controllers
{
	[Log]
	[LogActionFilter]
	public class ImportController : ApiController
	{
		private readonly IEnhancedOrgService service;
		private readonly CrmLog log;

		private readonly CrmImporter crmImporter;

		public ImportController(IEnhancedOrgService service, CrmLog log)
		{
			this.service = service;
			this.log = log;
			crmImporter = new CrmImporter(service, log, new MigrationMetadataHelper(service, log));
		}

		// POST api/<controller>
		public async Task<IHttpActionResult> Post()
		{
			log.Log($"Importing data using Post ...");

			var exportedData = await Request.Content.ReadAsStringAsync();

			if (string.IsNullOrEmpty(exportedData))
			{
				return BadRequest("Exported data cannot be empty.");
			}

			log.Log($"Importing ...");
			var result = crmImporter.ImportRecords(exportedData);
			log.Log($"Finished importing data.");

			return Ok(result);
		}
	}
}