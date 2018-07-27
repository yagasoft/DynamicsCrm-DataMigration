using Microsoft.Xrm.Sdk.Metadata;

namespace LinkDev.DataMigration.WebApp.Models.Metadata
{
	public class FieldMetadata
	{
		public string LogicalName { get; set; }
		public string DisplayName { get; set; }
		public AttributeTypeCode? Type { get; set; }
	}
}
