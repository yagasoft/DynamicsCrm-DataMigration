#region Imports

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using LinkDev.DataMigration.WebApp.Helpers;
using LinkDev.DataMigration.WebApp.Models.Export;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Model;

#endregion

namespace LinkDev.DataMigration.WebApp.BLL
{
	[Log]
	public class CrmExporter
	{
		private readonly IEnhancedOrgService service;
		private readonly CrmLog log;
		private readonly MigrationMetadataHelper metadataHelper;

		private readonly string[] ownerFields =
		{
			"createdby",
			"createdonbehalfby",
			"modifiedby",
			"modifiedonbehalfby",
			"ownerid",
			"owningbusinessunit",
			"owninguser",
			"owningteam"
		};

		private ExportOptions options;

		private readonly IDictionary<Guid, ExportedEntityDefinition> recordsMap =
			new Dictionary<Guid, ExportedEntityDefinition>();

		private readonly IDictionary<ExportedRelationDefinition, List<EntityReference>> relationsMap =
			new Dictionary<ExportedRelationDefinition, List<EntityReference>>();

		private readonly IDictionary<string, string> queries = new Dictionary<string, string>();

		public CrmExporter(IEnhancedOrgService service, CrmLog log, MigrationMetadataHelper metadataHelper)
		{
			this.service = service;
			this.log = log;
			this.metadataHelper = metadataHelper;
		}

		public string ExportRecords(ExportConfiguration configuration)
		{
			try
			{
				log.Log(new LogEntry("Exporting records using provided configuration.", information: configuration.ToJson()));

				options = configuration.ExportOptions;

				log.Log($"Going over {configuration.Records.Count} record definitions in parallel ...");
				Parallel.ForEach(configuration.Records,
					new ParallelOptions
					{
						MaxDegreeOfParallelism = CrmService.Threads
					},
					recordsConfig => LoadEntities(recordsConfig));
				log.Log($"Finished going over record definitions.");

				if (configuration.ExportOptions.IsExcludeOwner == true)
				{
					log.Log("Removing owner values from all records ...");
					foreach (var record in recordsMap.Values
						.Select(e => e.Record)
						.Where(e => e.Attributes.Keys.Intersect(ownerFields).Any()))
					{
						var recordOwnerFields = record.Attributes.Keys.Intersect(ownerFields).ToArray();

						foreach (var field in recordOwnerFields)
						{
							record.Attributes.Remove(field);
						}
						log.Log($"Removed '${recordOwnerFields.Aggregate((f1, f2) => $"{f1},{f2}")}'.");
					}
					log.Log("Finished removing owner values from all records.");
				}

				log.Log("Clearing formatted values from all records ...");
				foreach (var formattedValues in recordsMap.Values
					.Select(e => e.Record.FormattedValues)
					.Where(f => f.Any()))
				{
					formattedValues.Clear();
				}
				log.Log("Finished cleared formatted values from all records.");

				var exportedData =
					new ExportedData
					{
						Configuration = configuration,
						EntityDefinition = recordsMap,
						RelationDefinition = relationsMap,
						Queries = queries
					};

				log.Log("Serialising exported data ...");
				var serialisedData = configuration.ExportOptions.IsCompressData == true
					? Helpers.Helpers.Compress(exportedData)
					: Encoding.UTF8.GetString(Helpers.Helpers.Serialise(exportedData));
				log.Log("Finished serialising exported data.");

				log.Log($"Finished exporting records using provided configuration.");

				return serialisedData;
			}
			catch (AggregateException exception)
			{
				throw new Exception(exception.InnerExceptions
					.Select(e => $"{e.GetType()} => {e.Message}"
						+ $"{(e.InnerException == null ? "" : $"{e.InnerException.GetType()} => {e.InnerException.Message}")}")
					.Aggregate((e1, e2) => $"{e1} ||| {e2}"));
			}
		}

		private IEnumerable<EntityReference> LoadEntities(Record recordsConfig, Guid? recordId = null,
			string relationId = null)
		{
			log.Log(new LogEntry($"Exporting '{recordsConfig.LogicalName}' records using provided configuration ...",
				information: recordsConfig.ToJson()));

			if (recordsConfig.IsUseAlternateKeysForRecord == true)
			{
				VerifyKeysInFetchXml(recordsConfig);
			}

			var records = RetrieveRecords(recordsConfig, recordId);

			foreach (var record in records)
			{
				ProcessCrmRecord(recordsConfig, relationId, record);
			}

			var recordRefs = records.Select(r => r.ToEntityReference()).ToArray();
			log.Log($"Finished exporting '{recordsConfig.LogicalName}'"
				+ $" records ({recordRefs.Length}) using provided configuration.");

			return recordRefs;
		}

		private void VerifyKeysInFetchXml(Record recordsConfig)
		{
			log.Log($"Verifying keys exist in record ...");

			log.Log($"Retrieving keys for {recordsConfig.LogicalName} ...");
			var keys = metadataHelper.GetAlternateKeys(recordsConfig.LogicalName)?.ToArray();
			log.Log(
				$"Finished retrieving keys for {recordsConfig.LogicalName}. Keys: {keys?.Aggregate((e1, e2) => $"{e1},{e2}")}");

			if (keys == null)
			{
				log.LogWarning($"Can't find alterate keys definition for entity: '{recordsConfig.LogicalName}'.");
				return;
			}

			foreach (var key in keys)
			{
				if (!Regex.IsMatch(recordsConfig.FetchXml, $"<attribute\\s*name\\s*=\\s*(\"|'){key}(\"|')")
					&& Regex.IsMatch(recordsConfig.FetchXml, $"<attribute\\s*name\\s*=\\s*(\"|').*(\"|')"))
				{
					throw new Exception($"Can't find alterate key '{key}' in FetchXML of entity {recordsConfig.LogicalName}.");
				}
			}

			log.Log($"Finished verifying keys exist in record.");
		}

		private List<Entity> RetrieveRecords(Record recordsConfig, Guid? recordId)
		{
			log.Log($"Injecting ID field in FetchXML ...");
			recordsConfig.FetchXml = InjectIdFieldInFetchXml(recordsConfig.FetchXml, recordsConfig.LogicalName);
			log.Log($"Finished injecting ID field in FetchXML.");

			log.Log($"Cloning query ...");
			var query = service.CloneQuery(new FetchExpression(recordsConfig.FetchXml));
			log.Log($"Finished cloning query.");

			// replace recordId placeholders with the record ID if given
			if (recordId != null)
			{
				foreach (var values in query.LinkEntities
					.SelectMany(l => l.LinkCriteria.Conditions
						.Where(c => c.Values.Any(v => v is Guid vs && vs == Guid.Empty)))
					.Select(c => c.Values))
				{
					// ToArray: can't modify collection inside loop
					foreach (var value in values.Where(v => v is Guid vs && vs == Guid.Empty).ToArray())
					{
						values.Remove(value);
						values.Add(recordId);
					}
				}
			}

			log.Log($"Retrieving records using FetchXML for '{recordsConfig.LogicalName}' ...");
			var records = service.RetrieveMultiple<Entity>(query).Distinct(new EntityComparer()).ToList();
			log.Log($"Finished retrieving records using FetchXML for '{recordsConfig.LogicalName}'. Found {records.Count}.");

			return records;
		}

		private string InjectIdFieldInFetchXml(string fetchXml, string logicalName)
		{
			log.Log(new LogEntry($"Injecting ID field in FetchXML ...", information: fetchXml));

			log.Log($"Retrieving ID field for {logicalName} ...");
			var idFieldName = metadataHelper.GetIdFieldName(logicalName);
			log.Log($"Finished retrieving ID field for {logicalName}. Field: {idFieldName}.");

			// if there is an ID attribute already or retrieving all attributes, exit
			if (Regex.IsMatch(fetchXml, $"<attribute\\s*name\\s*=\\s*(\"|')${idFieldName}(\"|')")
				|| !Regex.IsMatch(fetchXml, $"<attribute\\s*name\\s*=\\s*(\"|').*(\"|')"))
			{
				log.Log($"Field already exists in FetchXML.");
				return fetchXml;
			}

			var doc = new XmlDocument();
			doc.LoadXml(fetchXml);
			var parent = doc.SelectSingleNode("/fetch/entity");

			// clear attributes
			var attributeNodes = parent?.SelectNodes("//entity/attribute");

			if (attributeNodes != null)
			{
				foreach (var node in attributeNodes.Cast<XmlNode>())
				{
					parent.RemoveChild(node);
				}
			}

			var stringBuilder = new StringBuilder();
			stringBuilder.Append("<fetch><entity>");
			stringBuilder.Append($"<attribute name='{idFieldName}' />");
			stringBuilder.Append("</fetch></entity>");

			var xmlParser = new XmlDocument();
			xmlParser.LoadXml(stringBuilder.ToString());

			var newXmlNodes = xmlParser.SelectSingleNode("/fetch/entity")?.ChildNodes.Cast<XmlNode>();

			if (newXmlNodes != null)
			{
				foreach (var node in newXmlNodes)
				{
					var newNode = parent?.OwnerDocument?.ImportNode(node, true);

					if (newNode != null)
					{
						parent.AppendChild(newNode);
					}
				}
			}

			var result = doc.OuterXml;
			log.Log(new LogEntry($"Finished injecting ID field in FetchXML.", information: result));

			return result;
		}

		private void ProcessCrmRecord(Record recordsConfig, string relationId, Entity record)
		{
			log.Log($"Processing record '{record.LogicalName}':'{record.Id}' ...");

			if (record.Id == Guid.Empty)
			{
				throw new Exception("Exported record contains an invalid GUID.");
			}

			recordsMap.TryGetValue(record.Id, out var entityDef);
			// give priority to root-level definitions
			var isFoundInRoot = entityDef == null || !string.IsNullOrEmpty(entityDef.RelationId);

			if (isFoundInRoot)
			{
				entityDef = recordsMap[record.Id] =
					new ExportedEntityDefinition
					{
						Record = record,
						IsCreate = recordsConfig.IsCreateNewRecords == true,
						IsUpdate = recordsConfig.IsUpdateExistingRecords == true,
						IsDeleteObsolete = recordsConfig.IsDeleteObsoleteRecords == true,
						IsUseAlternateKeys = recordsConfig.IsUseAlternateKeysForRecord == true,
						IsUseAlternateKeysForLookups = recordsConfig.IsUseAlternateKeysForLookups == true,
						RelationId = relationId
					};
				log.Log(new LogEntry($"Entity definition ...",
					information: $"IsCreate = {recordsConfig.IsCreateNewRecords},"
						+ $"IsUpdate = {recordsConfig.IsUpdateExistingRecords},"
						+ $"IsDeleteObsolete = {recordsConfig.IsDeleteObsoleteRecords},"
						+ $"IsUseAlternateKeys = {recordsConfig.IsUseAlternateKeysForRecord},"
						+ $"IsUseAlternateKeysForLookups = {recordsConfig.IsUseAlternateKeysForLookups},"
						+ $"RelationId = {relationId}"));

				var queryKey = recordsConfig.FetchXml.GetHashCode().ToString();
				queries[queryKey] = recordsConfig.FetchXml;
				entityDef.QueryKey = queryKey;

				if (recordsConfig.ValuesMapping?.ValueMappings?.Any() == true)
				{
					MapValues(recordsConfig, record);
				}

				if (recordsConfig.IsUseAlternateKeysForRecord == true)
				{
					log.Log("Filling alternate key values ...");
					record.KeyAttributes.AddRange(GetKeyValues(record));
					log.Log("Finished filling alternate key values.");
				}
			}

			if (recordsConfig.Relations?.Count > 0)
			{
				log.Log($"Going over {recordsConfig.Relations.Count} relations for {record.Id} ...");
				Parallel.ForEach(recordsConfig.Relations,
					new ParallelOptions
					{
						MaxDegreeOfParallelism = CrmService.Threads
					},
					relation =>
					{
						log.Log($"Processing relation '{relation.SchemaName}' ...");
						var relationQueryKey = relation.EntityDefinition.FetchXml.GetHashCode().ToString();
						queries[relationQueryKey] = relation.EntityDefinition.FetchXml;
						LoadRelation(relation, record, relationQueryKey, entityDef);
						log.Log($"Finished processing relation '{relation.SchemaName}'.");
					});
				log.Log($"Finished going over {recordsConfig.Relations.Count} relations for {record.Id}.");
			}

			if (recordsConfig.IsUseAlternateKeysForLookups == true)
			{
				FillKeysForLookups(record);
			}

			if (isFoundInRoot)
			{
				var targetName = recordsConfig.ValuesMapping?.TargetEntityName;

				if (!string.IsNullOrEmpty(targetName))
				{
					record.LogicalName = targetName;
					log.Log($"Replaced entity logical name with '{targetName}'.");
				}
			}

			log.Log($"Finished processing record '{record.LogicalName}':'{record.Id}'.");
		}

		private void MapValues(Record recordsConfig, Entity record)
		{
			log.Log("Mapping values in record ...");
			recordsConfig.ValuesMapping.Require(nameof(recordsConfig.ValuesMapping));

			foreach (var valueMapping in recordsConfig.ValuesMapping.ValueMappings)
			{
				var sourceValue = record.GetAttributeValue<object>(valueMapping.SourceField);
				var sourceType = (AttributeTypeCode?)valueMapping.SourceFieldType;
				var destinationType = (AttributeTypeCode?)valueMapping.DestinationFieldType ?? sourceType;

				if (valueMapping.IsIgnoreValues == true)
				{
					record[valueMapping.DestinationField] = ConvertCrmValue(sourceValue, destinationType);

					if (sourceValue != null)
					{
						record.Attributes.Remove(valueMapping.SourceField);
					}

					log.Log($"Moved {valueMapping.SourceField}'s value to {valueMapping.DestinationField}: '{sourceValue}'.");
				}
				else
				{
					if (valueMapping.SourceValue == "*"
						|| (sourceValue == null && string.IsNullOrEmpty(valueMapping.SourceValue))
						|| sourceValue?.Equals(valueMapping.SourceValue) == true)
					{
						var mappedValue = GetCrmTypedValue(destinationType, valueMapping.DestinationValue);
						record[valueMapping.DestinationField] = mappedValue;
						log.Log($"Replaced {valueMapping.DestinationField}'s value with '{mappedValue}'.");
					}
				}
			}

			log.Log("Finished mapping values in record.");
		}

		private void LoadRelation(Relation relation, Entity record, string relationQueryKey,
			ExportedEntityDefinition entityDef)
		{
			var doc = new XmlDocument();
			doc.LoadXml(relation.EntityDefinition.FetchXml);

			// add the necessary joins to load only related records
			var parent = doc.SelectSingleNode("/fetch/entity");
			var xmlParser = new XmlDocument();
			InjectRelationLinkInFetchXml(record.ToEntityReference(), relation, xmlParser, parent);
			var newEntityDef = Record.FromJson(relation.EntityDefinition.ToJson());
			newEntityDef.FetchXml = doc.OuterXml;

			var recordsRef = LoadEntities(newEntityDef, record.Id, relation.DefId).ToList();

			var relationDefinition =
				new ExportedRelationDefinition
				{
					Id = relation.DefId,
					ParentReference = record.ToEntityReference(),
					IsUseKeysForParent = entityDef.IsUseAlternateKeys,
					RelationshipInfo = new Relationship(relation.SchemaName) { PrimaryEntityRole = EntityRole.Referenced },
					RelatedLogicalName = newEntityDef.LogicalName,
					IsDisassociateObsolete = relation.IsDisassociateObsoleteRelations == true,
					IsDeleteObsolete = relation.IsDeleteObsoleteRelations == true,
					QueryKey = relationQueryKey
				};
			relationsMap[relationDefinition] = recordsRef;
			log.Log(new LogEntry($"Entity definition ...",
				information: $"Id = {relationDefinition.Id},"
					+ $"IsCreate = '{relationDefinition.ParentReference.LogicalName}':'{relationDefinition.ParentReference.Id}',"
					+ $"IsUseKeysForParent = {relationDefinition.IsUseKeysForParent},"
					+ $"RelatedLogicalName = {relationDefinition.RelatedLogicalName},"
					+ $"IsDisassociateObsolete = {relationDefinition.IsDisassociateObsolete},"
					+ $"IsDeleteObsolete = {relationDefinition.IsDeleteObsolete}"
					+ $"relationQueryKey = {relationDefinition.QueryKey}"));
		}

		private void InjectRelationLinkInFetchXml(EntityReference recordRef, Relation relation,
			XmlDocument xmlParser, XmlNode parent)
		{
			log.Log(new LogEntry($"Injecting relation link in FetchXML ...", information: parent.OuterXml));

			var stringBuilder = new StringBuilder();
			stringBuilder.Append("<fetch><entity>");

			log.Log($"Retrieving relation metadata for {relation.SchemaName} ...");
			var relationMetadata = metadataHelper.GetRelationMetaData(recordRef.LogicalName, relation.SchemaName);
			log.Log($"Finished retrieving relation metadata for {relation.SchemaName}.");

			if (relationMetadata.Type == MetadataHelpers.RelationType.ManyToManyRelationships)
			{
				stringBuilder.Append($"<link-entity name='{relationMetadata.IntersectingEntityName}'"
					+ $" from='{relationMetadata.Entity1FieldName}' to='{relationMetadata.Entity1FieldName}' intersect='true'>");
				stringBuilder.Append($"<link-entity name='{relationMetadata.Entity2Name}'"
					+ $" from='{relationMetadata.Entity2FieldName}' to='{relationMetadata.Entity2FieldName}'>");

				stringBuilder.Append("<filter>");
				stringBuilder.Append(
					$"<condition attribute='{relationMetadata.Entity2FieldName}' operator='eq' value='{Guid.Empty}' />");
			}
			else
			{
				stringBuilder.Append($"<link-entity name='{relationMetadata.Entity1Name}'"
					+ $" from='{relationMetadata.Entity1FieldName}' to='{relationMetadata.Entity2FieldName}'>");

				stringBuilder.Append("<filter>");
				stringBuilder.Append(
					$"<condition attribute='{relationMetadata.Entity1FieldName}' operator='eq' value='{Guid.Empty}' />");
			}
			stringBuilder.Append("</filter>");

			stringBuilder.Append("</link-entity>");

			if (relationMetadata.Type == MetadataHelpers.RelationType.ManyToManyRelationships)
			{
				stringBuilder.Append("</link-entity>");
			}

			stringBuilder.Append("</entity></fetch>");

			xmlParser.LoadXml(stringBuilder.ToString());

			var newXmlNodes = xmlParser.SelectSingleNode("/fetch/entity")?.ChildNodes.Cast<XmlNode>();

			if (newXmlNodes != null)
			{
				foreach (var node in newXmlNodes)
				{
					var newNode = parent?.OwnerDocument?.ImportNode(node, true);

					if (newNode != null)
					{
						parent.AppendChild(newNode);
					}
				}
			}

			log.Log(new LogEntry($"Finished injecting relation link in FetchXML.", information: parent.OuterXml));
		}

		private void FillKeysForLookups(Entity record)
		{
			log.Log($"Filling alternate keys in lookups for {record} ...");

			var keyedLookups = record.Attributes
				.Select(a => a.Value).OfType<EntityReference>()
				.Select(a => new
							 {
								 LookupRef = a,
								 Keys = metadataHelper.GetAlternateKeys(a.LogicalName).ToArray()
							 })
				.Where(a => a.Keys.Any())
				.ToArray();

			Parallel.ForEach(keyedLookups,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				keyedLookup =>
				{
					var lookupRef = keyedLookup.LookupRef;
					var lookupKeys = keyedLookup.Keys;
					var id = $"'{lookupRef.LogicalName}':'{lookupRef.Id}'";
					log.Log($"Retrieving lookup record {id} ...");
					var lookupRecord = service.Retrieve(lookupRef.LogicalName, lookupRef.Id, new ColumnSet(lookupKeys));
					log.Log($"Finished retrieving lookup record {id}.");

					lookupRef.KeyAttributes.AddRange(lookupKeys
						.ToDictionary(key => key, key => lookupRecord.GetAttributeValue<object>(key)));
				});

			log.Log($"Finished filling alternate keys in lookups for {record}.");
		}

		private IDictionary<string, object> GetKeyValues(Entity record)
		{
			log.Log($"Retrieving keys for {record.LogicalName} ...");
			var keys = metadataHelper.GetAlternateKeys(record.LogicalName)?.ToArray();
			log.Log($"Finished retrieving keys for {record.LogicalName}. Keys: {keys?.Aggregate((e1, e2) => $"{e1},{e2}")}");

			if (keys == null)
			{
				log.LogWarning($"Can't find alterate keys definition for entity: '{record.LogicalName}'.");
				return new Dictionary<string, object>();
			}

			return keys.ToDictionary(k => k, record.GetAttributeValue<object>);
		}

		private object GetCrmTypedValue(AttributeTypeCode? type, string value)
		{
			type.Require(nameof(type));

			if (string.IsNullOrEmpty(value))
			{
				return null;
			}

			try
			{
				switch (type)
				{
					case AttributeTypeCode.State:
					case AttributeTypeCode.Status:
					case AttributeTypeCode.Boolean:
					case AttributeTypeCode.Picklist:
						return new OptionSetValue(int.Parse(value));

					case AttributeTypeCode.DateTime:
						return DateTime.ParseExact(value, "yyyy-MM-dd_hh-mm-ss_tt",
							new DateTimeFormatInfo(), DateTimeStyles.AssumeLocal);

					case AttributeTypeCode.Decimal:
						return decimal.Parse(value);

					case AttributeTypeCode.Double:
						return double.Parse(value);

					case AttributeTypeCode.Integer:
						return int.Parse(value);

					case AttributeTypeCode.Money:
						return new Money(decimal.Parse(value));

					case AttributeTypeCode.Lookup:
					case AttributeTypeCode.Customer:
					case AttributeTypeCode.Owner:
						var split = value.Split(':');

						if (split.Length < 2)
						{
							throw new FormatException($"Entity reference value is not in the correct format '{value}'.");
						}

						return new EntityReference(split[0], Guid.Parse(split[1]));

					case AttributeTypeCode.Memo:
					case AttributeTypeCode.String:
						return value;

					case AttributeTypeCode.Uniqueidentifier:
						return Guid.Parse(value);

					case AttributeTypeCode.BigInt:
					case AttributeTypeCode.CalendarRules:
					case AttributeTypeCode.Virtual:
					case AttributeTypeCode.ManagedProperty:
					case AttributeTypeCode.EntityName:
					case AttributeTypeCode.PartyList:
					case null:
						throw new Exception($"Unsupported value type '{type}'.");

					default:
						throw new ArgumentOutOfRangeException(nameof(type), type, null);
				}
			}
			catch (Exception e)
			{
				throw new FormatException($"Failed to convert '{value}' to '{type}'.", e);
			}
		}

		private object ConvertCrmValue(object value, AttributeTypeCode? destinationType)
		{
			destinationType.Require(nameof(destinationType));

			if (value == null)
			{
				return null;
			}

			return GetCrmTypedValue(destinationType, GetCrmStringValue(value));
		}

		private string GetCrmStringValue(object value)
		{
			switch (value)
			{
				case OptionSetValue optionSetValue:
					return optionSetValue.Value.ToString();

				case DateTime dateTime:
					return dateTime.ToLocalTime().ToString("yyyy-MM-dd_hh-mm-ss_tt");

				case decimal num:
					return num.ToString();

				case double num:
					return num.ToString();

				case int num:
					return num.ToString();

				case Money money:
					return money.Value.ToString();

				case EntityReference reference:
					return $"{reference.LogicalName}:{reference.Id}";

				case string str:
					return str;

				case Guid guid:
					return guid.ToString();

				default:
					throw new ArgumentOutOfRangeException(nameof(value), value.GetType().FullName, null);
			}
		}
	}
}
