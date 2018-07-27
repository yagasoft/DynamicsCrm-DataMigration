using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Model;

namespace LinkDev.DataMigration.WebApp.Models.Export
{
    public class ExportedData
    {
	    public ExportConfiguration Configuration { get; set; }
	    public IDictionary<Guid, ExportedEntityDefinition> EntityDefinition { get; set; }
	    public IDictionary<ExportedRelationDefinition, List<EntityReference>> RelationDefinition { get; set; }
	    public IDictionary<string, string> Queries { get; set; }
    }
}
