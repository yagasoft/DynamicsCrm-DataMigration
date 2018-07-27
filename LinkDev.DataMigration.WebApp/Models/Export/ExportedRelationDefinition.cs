#region Imports

using System;
using Microsoft.Xrm.Sdk;

#endregion

namespace LinkDev.DataMigration.WebApp.Models.Export
{
	public class ExportedRelationDefinition : IEquatable<ExportedRelationDefinition>
	{
		public string Id { get; set; }
		public EntityReference ParentReference { get; set; }
		public bool IsUseKeysForParent { get; set; }
		public Relationship RelationshipInfo { get; set; }
		public string RelatedLogicalName { get; set; }
		public bool IsDeleteObsolete { get; set; }
		public bool IsDisassociateObsolete { get; set; }
		public string QueryKey { get; set; }

		public bool Equals(ExportedRelationDefinition other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			return ParentReference.Id.Equals(other.ParentReference.Id) && RelationshipInfo.Equals(other.RelationshipInfo);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if (obj.GetType() != GetType())
			{
				return false;
			}

			return Equals((ExportedRelationDefinition)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (ParentReference.Id.GetHashCode() * 397) ^ RelationshipInfo.GetHashCode();
			}
		}
	}
}
