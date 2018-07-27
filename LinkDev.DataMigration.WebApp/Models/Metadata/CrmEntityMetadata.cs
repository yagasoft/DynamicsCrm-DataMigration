using System.Collections.Generic;
using LinkDev.Libraries.Common;

namespace LinkDev.DataMigration.WebApp.Models.Metadata
{
    public class CrmEntityMetadata
    {
	    public string LogicalName { get; set; }
	    public string DisplayName { get; set; }
	    public string IdFieldName { get; set; }
	    public List<string> AlternateKeyNames { get; set; }
	    public List<FieldMetadata> FieldsMetaData { get; set; }
	    public List<RelationMetadata> RelationsMetaData { get; set; }
    }
}
