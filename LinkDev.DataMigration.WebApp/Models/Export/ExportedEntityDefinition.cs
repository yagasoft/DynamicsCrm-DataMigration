using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace LinkDev.DataMigration.WebApp.Models.Export
{
    public class ExportedEntityDefinition
    {
	    public Entity Record { get; set; }
	    public bool IsCreate { get; set; }
	    public bool IsUpdate { get; set; }
	    public bool IsDeleteObsolete { get; set; }
	    public bool IsUseAlternateKeys { get; set; }
	    public bool IsUseAlternateKeysForLookups { get; set; }
		public string QueryKey { get; set; }
	    public string RelationId { get; set; }
    }
}
