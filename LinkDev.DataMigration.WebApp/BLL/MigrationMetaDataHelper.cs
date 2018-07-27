#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.DataMigration.WebApp.Models.Metadata;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Xrm.Sdk.Metadata;
using static LinkDev.Libraries.Common.Helpers;

#endregion

namespace LinkDev.DataMigration.WebApp.BLL
{
	[Log]
	public class MigrationMetadataHelper
	{
		private readonly IEnhancedOrgService service;
		private readonly CrmLog log;

		public MigrationMetadataHelper(IEnhancedOrgService service, CrmLog log)
		{
			this.service = service;
			this.log = log;
		}

		public IDictionary<string, string> GetEntityNames()
		{
			const string key = "MetaDataHelper.GetEntityNames";
			var cachedEntityNames = GetFromMemCache<IDictionary<string, string>>(key);
			return cachedEntityNames ?? AddToMemCache(key, MetadataHelpers.GetEntityNames(service, CrmService.OrgId));
		}

		public CrmEntityMetadata GetEntityMetaData(string logicalName)
		{
			var key = $"MetaDataHelper.GetEntityMetaData|{logicalName}";
			var cachedEntityMetaData = GetFromMemCache<CrmEntityMetadata>(key);

			if (cachedEntityMetaData != null)
			{
				return cachedEntityMetaData;
			}

			var retrievedMetaData = MetadataHelpers.GetEntity(service, logicalName, CrmService.OrgId);

			return AddToMemCache(key,
				new CrmEntityMetadata
				{
					LogicalName = retrievedMetaData.LogicalName,
					DisplayName = retrievedMetaData.DisplayName?.UserLocalizedLabel?.Label,
					IdFieldName = retrievedMetaData.PrimaryIdAttribute,
					AlternateKeyNames = retrievedMetaData.Keys.SelectMany(k => k.KeyAttributes).ToList(),
					FieldsMetaData = retrievedMetaData.Attributes
						.Where(a => a.DisplayName?.UserLocalizedLabel?.Label != null)
						.Select(a => new FieldMetadata
									 {
										 LogicalName = a.LogicalName,
										 DisplayName = a.DisplayName.UserLocalizedLabel.Label,
										 Type = a.AttributeType
									 }).ToList(),
					RelationsMetaData = MetadataHelpers.BuildRelationMetadata(retrievedMetaData).ToList()
				});
		}

		public RelationMetadata GetRelationMetaData(string logicalName, string schemaName)
		{
			var key = $"MetaDataHelper.GetRelationMetaData|{logicalName}|{schemaName}";
			var cachedEntityMetaData = GetFromMemCache<RelationMetadata>(key);

			if (cachedEntityMetaData != null)
			{
				return cachedEntityMetaData;
			}

			var retrievedMetaData = MetadataHelpers.GetRelation(service, logicalName, schemaName, CrmService.OrgId);

			if (retrievedMetaData == null)
			{
				throw new Exception($"Couldn't find metadata for relation '{schemaName}' in entity '{logicalName}'.");
			}

			return AddToMemCache(key, retrievedMetaData);
		}

		public IEnumerable<RelationMetadata> GetRelationsMetaData(string logicalName, MetadataHelpers.RelationType type)
		{
			var key = $"MetaDataHelper.GetRelationMetaData|{logicalName}|{type}";
			var cachedEntityMetaData = GetFromMemCache<IEnumerable<RelationMetadata>>(key);

			if (cachedEntityMetaData != null)
			{
				return cachedEntityMetaData;
			}

			var retrievedMetaData = MetadataHelpers.GetEntityRelations(service, logicalName, type, CrmService.OrgId).ToList();

			if (retrievedMetaData.Count <= 0)
			{
				throw new Exception($"Couldn't find metadata for relations of type '{type}' in entity '{logicalName}'.");
			}

			return AddToMemCache(key, retrievedMetaData);
		}

		public IEnumerable<string> GetAlternateKeys(string logicalName)
		{
			var key = $"MetaDataHelper.GetAlternateKeys|{logicalName}";
			var cachedEntityMetaData = GetFromMemCache<IEnumerable<string>>(key);

			if (cachedEntityMetaData != null)
			{
				return cachedEntityMetaData;
			}

			var retrievedMetaData = MetadataHelpers
				.GetEntityAttribute<EntityKeyMetadata[]>(service,
					logicalName, MetadataHelpers.EntityAttribute.Keys, CrmService.OrgId);

			return AddToMemCache(key, retrievedMetaData?.SelectMany(m => m.KeyAttributes ?? new string[0]));
		}

		public string GetIdFieldName(string logicalName)
		{
			var key = $"MetaDataHelper.GetIdFieldName|{logicalName}";
			var cachedEntityMetaData = GetFromMemCache<string>(key);

			if (cachedEntityMetaData != null)
			{
				return cachedEntityMetaData;
			}

			var idFieldName = MetadataHelpers
				.GetEntityAttribute<string>(service,
					logicalName, MetadataHelpers.EntityAttribute.PrimaryIdAttribute, CrmService.OrgId);

			return AddToMemCache(key, idFieldName);
		}
	}
}
