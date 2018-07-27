#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Model;

#endregion

namespace LinkDev.DataMigration.WebApp.BLL
{
	[Log]
	public class CrmImporter
	{
		private enum RecordsFilterMode
		{
			UniqueInSource,
			UniqueInDestination,
			Common
		}

		private readonly IEnhancedOrgService service;
		private readonly OrganizationServiceContext context;
		private readonly CrmLog log;
		private readonly MigrationMetadataHelper metadataHelper;

		private Action<int, int, IDictionary<OrganizationRequest, ExecuteBulkResponse>> BulkFinishHandler =>
			(currentBulk, bulkCount, responses) => { log.Log($"Finished bulk {currentBulk} / {bulkCount}."); };

		private ExportedData data;
		private IDictionary<Guid, ExportedEntityDefinition> recordsMap;
		private IDictionary<ExportedRelationDefinition, List<EntityReference>> relationsMap;
		private IDictionary<string, string> queries;

		private readonly IDictionary<string, List<Entity>> retrieveCache = new ConcurrentDictionary<string, List<Entity>>();

		private ExportConfiguration config;
		private ImportOptions options;
		private int batchSize;

		private IDictionary<Guid, Guid> idsMap;

		public CrmImporter(IEnhancedOrgService service, CrmLog log, MigrationMetadataHelper metadataHelper)
		{
			this.service = service;
			context = new OrganizationServiceContext(service);
			this.log = log;
			this.metadataHelper = metadataHelper;
		}

		public bool ImportRecords(string rawData)
		{
			var splitRawData = rawData.Split(new[] { "<|||>" }, StringSplitOptions.RemoveEmptyEntries);

			if (!splitRawData.Any())
			{
				throw new Exception("Data given is empty.");
			}

			var rawStringData = splitRawData[0];

			if (splitRawData.Length > 1 && splitRawData[1] == "TRUE")
			{
				data = Helpers.Helpers.Decompress<ExportedData>(rawStringData);

				if (splitRawData.Length > 2)
				{
					options = ImportOptions.FromJson(splitRawData[2]);
				}
			}
			else
			{
				data = Helpers.Helpers.Deserialise<ExportedData>(Encoding.UTF8.GetBytes(rawStringData));
			}

			if (data?.EntityDefinition == null)
			{
				log.LogWarning("Import data is empty, skipping ...");
				return true;
			}

			log.Log($"Importing {data.EntityDefinition?.Count} records.");
			log.Log($"Importing {data.RelationDefinition?.Count} relations.");

			Initialise();

			ProcessObsoleteRecordsRemoval();

			var dependencyRequests = ProcessDependencies(recordsMap.Values.ToArray()).ToList();

			var requests = GetSyncRequests();

			idsMap = requests
				.ToDictionary(e => GetIdFromRequestResponse(e) ?? Guid.Empty, e => GetIdFromRequestResponse(e) ?? Guid.Empty);

			foreach (var key in recordsMap.Keys.Except(idsMap.Keys))
			{
				idsMap[key] = key;
			}

			idsMap[Guid.Empty] = Guid.Empty;

			var keyedRecords = recordsMap
				.Where(e => e.Value.IsUseAlternateKeys && e.Value.Record.KeyAttributes.Any())
				.Select(e => e.Value.Record).ToArray();

			log.Log($"Clearing IDs of ${keyedRecords.Length} keyed records ...");
			foreach (var record in keyedRecords)
			{
				record.Id = Guid.Empty;
			}

			var responses = ExecuteRequests(requests, "Failed in record sync step.");

			MapOldIdsToNewIds(requests, responses.Values);

			UpdateRelationRequestIds();
			ProcessObsoleteRelationsRemoval();
			
			UpdateLookupRequestIds(dependencyRequests);
			ExecuteRequests(dependencyRequests.Cast<OrganizationRequest>().ToList(),
				"Failed in lookup dependencies update step.");

			var associateRequests = CreateAssociateRequests().ToArray();
			ExecuteRequests(associateRequests.Cast<OrganizationRequest>().ToList(), "Failed in association step.",
				faultMessage => faultMessage != null && !faultMessage.Contains("Cannot insert duplicate key"));

			return true;
		}

		private void Initialise()
		{
			log.Log("Initialising data import parameters ...");
			recordsMap = data.EntityDefinition;

			if (recordsMap.Any(r => r.Key == Guid.Empty))
			{
				throw new Exception("Exported records contains an invalid GUID.");
			}

			relationsMap = data.RelationDefinition;
			queries = data.Queries;
			config = data.Configuration;
			batchSize = (int?)options?.BulkSize ?? 500;
			log.Log("Initialised data import parameters.");
		}

		private void ProcessObsoleteRecordsRemoval()
		{
			log.Log("Grouping records to compare for obsolete removal by logical name ...");
			var recordsToCompare = recordsMap
				.Where(r => r.Value.IsDeleteObsolete)
				.GroupBy(r => r.Value.Record.LogicalName)
				.ToDictionary(g => g.Key, g => g.Select(e => e.Value));

			var deleteRequests = new List<DeleteRequest>();

			log.Log("Deleting obsolete records ...");
			Parallel.ForEach(recordsToCompare,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				pair => deleteRequests.AddRange(GetDeleteRequests(pair.Key, pair.Value)));

			if (deleteRequests.Any())
			{
				ExecuteRequests(deleteRequests.Cast<OrganizationRequest>().ToList(),
					"Failed in obsolete record removal step.");
			}
		}

		private IEnumerable<DeleteRequest> GetDeleteRequests(string logicalName,
			IEnumerable<ExportedEntityDefinition> recordDefinitions)
		{
			return GetFilteredRecords(logicalName, recordDefinitions, RecordsFilterMode.UniqueInDestination)
				.Select(reference => new DeleteRequest { Target = reference });
		}

		private IEnumerable<EntityReference> GetFilteredRecords(string logicalName,
			IEnumerable<ExportedEntityDefinition> recordDefinitions, RecordsFilterMode mode)
		{
			log.Log($"Getting filtered records ...");

			var uniqueRecords = new List<EntityReference>();

			log.Log($"Getting existing records for '{logicalName}'...");

			log.Log($"Getting ID field name ...");
			var idField = MetadataHelpers
				.GetEntityAttribute<string>(service, logicalName, MetadataHelpers.EntityAttribute.PrimaryIdAttribute,
					CrmService.OrgId);
			log.Log($"ID field name: {idField}.");

			log.Log($"Getting alternate key names ...");
			var alternateKeys = metadataHelper.GetAlternateKeys(logicalName).ToArray();
			log.Log($"Found {alternateKeys.Length} keys.");

			var definitions = recordDefinitions.ToArray();
			var idDefinitions = definitions.Where(d => !d.IsUseAlternateKeys || !d.Record.KeyAttributes.Any()).ToArray();
			var alternateKeyDefinitions = definitions.Where(d => d.IsUseAlternateKeys && d.Record.KeyAttributes.Any()).ToArray();

			Parallel.ForEach(idDefinitions.GroupBy(d => d.QueryKey),
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				group =>
				{
					queries.TryGetValue(group.Key, out var fetchXml);
					fetchXml = BuildExistingRecordsFetchXml(fetchXml, idField);

					foreach (var relationKey in group.GroupBy(g => g.RelationId, g => g).Select(g => g.Key))
					{
						var updatedFetchXml = UpdateRelatedConditionInFetchXml(fetchXml, relationKey);
						var existingRecords = RetrieveRecords(updatedFetchXml);

						switch (mode)
						{
							case RecordsFilterMode.UniqueInDestination:
								uniqueRecords.AddRange(existingRecords.Select(r => r.Id).Except(group.Select(d => d.Record.Id))
									.Select(id => new EntityReference(logicalName, id)));
								break;

							case RecordsFilterMode.UniqueInSource:
								uniqueRecords.AddRange(group.Select(d => d.Record.Id).Except(existingRecords.Select(r => r.Id))
									.Select(id => new EntityReference(logicalName, id)));
								break;

							case RecordsFilterMode.Common:
								uniqueRecords.AddRange(group.Select(d => d.Record.Id).Intersect(existingRecords.Select(r => r.Id))
									.Select(id => new EntityReference(logicalName, id)));
								break;

							default:
								throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unique records mode is out of range.");
						}
					}
				});

			Parallel.ForEach(alternateKeyDefinitions.GroupBy(d => d.QueryKey),
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				group =>
				{
					queries.TryGetValue(group.Key, out var fetchXml);
					fetchXml = BuildExistingRecordsFetchXml(fetchXml, idField, alternateKeys);

					foreach (var relationKey in group.GroupBy(g => g.RelationId, g => g).Select(g => g.Key))
					{
						var updatedFetchXml = UpdateRelatedConditionInFetchXml(fetchXml, relationKey);
						var existingRecords = RetrieveRecords(updatedFetchXml);

						var exportedRecords = group.Select(d => d.Record);
						IEnumerable<Guid> existingExceptExportedIds;

						switch (mode)
						{
							case RecordsFilterMode.UniqueInDestination:
								existingExceptExportedIds = existingRecords
									.Where(r => exportedRecords.All(e => alternateKeys.All(
										a =>
										{
											var firstValue = r.GetAttributeValue<object>(a);
											var secondValue = e.GetAttributeValue<object>(a);

											return (firstValue != null && secondValue != null && !firstValue.Equals(secondValue))
												|| (firstValue != secondValue);
										})))
									.Select(r => r.Id);
								break;

							case RecordsFilterMode.UniqueInSource:
								existingExceptExportedIds = exportedRecords
									.Where(r => existingRecords.All(e => alternateKeys.All(
										a =>
										{
											var firstValue = r.GetAttributeValue<object>(a);
											var secondValue = e.GetAttributeValue<object>(a);

											return (firstValue != null && secondValue != null && !firstValue.Equals(secondValue))
												|| (firstValue != secondValue);
										})))
									.Select(r => r.Id);
								break;

							default:
								throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unique records mode is out of range.");
						}

						uniqueRecords.AddRange(existingExceptExportedIds
							.Select(id => new EntityReference(logicalName, id)));
					}
				});

			log.Log($"Got existing records for '{logicalName}'. Count: {uniqueRecords.Count}");

			return uniqueRecords;
		}

		private string BuildExistingRecordsFetchXml(string fetchXml, string idField, params string[] columns)
		{
			fetchXml.RequireNotEmpty(nameof(fetchXml));
			idField.RequireNotEmpty(nameof(idField));

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

			stringBuilder.Append($"<attribute name='{idField}' />");

			foreach (var column in columns)
			{
				stringBuilder.Append($"<attribute name='{column}' />");
			}

			stringBuilder.Append("</entity></fetch>");

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

			return doc.OuterXml;
		}

		private string UpdateRelatedConditionInFetchXml(string fetchXml, string relationKey)
		{
			if (!string.IsNullOrEmpty(relationKey))
			{
				var relation = relationsMap.FirstOrDefault(r => r.Key.Id == relationKey).Key;

				if (relation == null)
				{
					throw new Exception($"Can't find relation definition '{relationKey}'.");
				}

				recordsMap.TryGetValue(relation.ParentReference.Id, out var parent);

				if (parent == null)
				{
					throw new Exception($"Can't find parent record '{relation.ParentReference.Id}'.");
				}

				if (relation.IsUseKeysForParent)
				{
					fetchXml = Regex.Replace(fetchXml,
						$"<condition attribute='.*?' operator='eq' value='{Guid.Empty}' />",
						match =>
						{
							var builder = new StringBuilder();

							foreach (var key in metadataHelper.GetAlternateKeys(relation.ParentReference.LogicalName))
							{
								var value = parent.Record.GetAttributeValue<object>(key);
								var valueString = "";
								var op = "eq";

								if (value != null)
								{
									if (value is EntityReference valueRef)
									{
										valueString = valueRef.Id.ToString();
									}
									else if (value is DateTime valueDateTime)
									{
										valueString = valueDateTime.ToString("yyyy-MM-dd");
										op = "on";
									}
									else if (value is Money valueMoney)
									{
										valueString = valueMoney.Value.ToString();
									}
									else if (value is OptionSetValue valueOption)
									{
										valueString = valueOption.Value.ToString();
									}
									else
									{
										valueString = value.ToString();
									}
								}

								builder.Append($"<condition attribute='{key}'"
									+ $" operator='{op}' value='{valueString}' />");
							}

							return builder.ToString();
						});
				}
				else
				{
					fetchXml = fetchXml.Replace(Guid.Empty.ToString(), parent.Record.Id.ToString());
				}
			}

			return fetchXml;
		}

		private List<Entity> RetrieveRecords(string fetchXml)
		{
			retrieveCache.TryGetValue(fetchXml, out var existingRecords);
			retrieveCache[fetchXml] = existingRecords = existingRecords
				?? service.RetrieveMultiple<Entity>(service.CloneQuery(new FetchExpression(fetchXml)))
					.Distinct(new EntityComparer()).ToList();
			return existingRecords;
		}

		private IEnumerable<UpdateRequest> ProcessDependencies(ExportedEntityDefinition[] recordDefs)
		{
			log.Log($"Processing lookup dependencies ...", LogLevel.Debug);

			var lookupUpdateRequests = new Dictionary<Guid, UpdateRequest>();
			var records = recordDefs.Select(d => d.Record).ToArray();

			log.Log("Extracting dependency records ...");
			var dependencyRecords = records
				.SelectMany(r => r.Attributes.Select(a => a.Value).OfType<EntityReference>().Select(a => a.Id))
				.Intersect(records.Select(r => r.Id)).ToArray();
			log.Log($"Found {dependencyRecords.Length} dependency records.");

			log.Log("Looping over records ...");
			// extract lookup dependencies and clear values to be updated later 
			Parallel.ForEach(recordDefs,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				recordDef =>
				{
					var record = recordDef.Record;

					log.Log($"Processing {record.Id} ...");
					var lookups = record.Attributes.Where(a => a.Value is EntityReference).ToArray();
					log.Log($"Found {lookups.Length} lookups.");

					var lookupsToDefer = lookups
						.Where(l =>
						{
							var lookup = (EntityReference)l.Value;
							return dependencyRecords.Contains(lookup.Id)
							|| (lookup.KeyAttributes.Any() && recordDef.IsUseAlternateKeysForLookups);
						}).ToArray();
					log.Log($"Found {lookupsToDefer.Length} lookups to defer.");

					var dependentLookups = lookups.Select(l => l.Value).OfType<EntityReference>()
						.Where(r => dependencyRecords.Contains(r.Id)).ToArray();
					log.Log($"Found {dependentLookups.Length} dependent lookups.");

					var keyedLookups = lookups.Select(l => l.Value).OfType<EntityReference>()
						.Where(r => !dependencyRecords.Contains(r.Id) && r.KeyAttributes.Any()).ToArray();
					log.Log($"Found {keyedLookups.Length} keyedLookups lookups.");

					// alternate keys won't be needed if the option is not set to use them
					if (recordDef.IsUseAlternateKeysForLookups)
					{
						log.Log("Clearing lookup IDs ...");

						// clear lookup IDs that don't exist in the exported records
						foreach (var lookup in keyedLookups)
						{
							lookup.Id = Guid.Empty;
						}
					}

					// TODO: check and remove invalid in remaining lookups if option is set

					log.Log("Looping over lookups to create update request ...");

					lock (lookupUpdateRequests)
					{
						foreach (var attribute in lookupsToDefer)
						{
							if (lookupUpdateRequests.TryGetValue(record.Id, out var updateRequest))
							{
								updateRequest.Target[attribute.Key] = attribute.Value;
							}
							else
							{
								lookupUpdateRequests.Add(record.Id,
									new UpdateRequest
									{
										Target = new Entity(record.LogicalName, record.Id)
												 {
													 Attributes = new AttributeCollection { attribute }
												 }
									});
							}

							record.Attributes.Remove(attribute.Key);
						}
					}
				});

			log.Log($"Lookup update requests count: {lookupUpdateRequests.Count}.");

			return lookupUpdateRequests.Values;
		}

		private List<OrganizationRequest> GetSyncRequests()
		{
			var requests = new List<OrganizationRequest>();

			requests.AddRange(CreateUpsertRequests());

			if (recordsMap.Values.Any(r => r.IsCreate && !r.IsUpdate))
			{
				requests.AddRange(CreateCreateRequests());
			}

			if (recordsMap.Values.Any(r => !r.IsCreate && r.IsUpdate))
			{
				requests.AddRange(CreateUpdateRequests());
			}
			return requests;
		}

		private IEnumerable<UpsertRequest> CreateUpsertRequests()
		{
			log.Log($"Creating Upsert requests for {recordsMap.Count} records ...");
			return recordsMap.Values
				.Where(r => r.IsCreate && r.IsUpdate)
				.Select(r => new UpsertRequest { Target = r.Record });
		}

		private IEnumerable<CreateRequest> CreateCreateRequests()
		{
			var recordsToCompare = recordsMap.Values
				.Where(r => r.IsCreate && !r.IsUpdate)
				.GroupBy(r => r.Record.LogicalName)
				.ToDictionary(g => g.Key, g => g.Select(e => e));

			var createRequests = new List<CreateRequest>();

			log.Log($"Creating Create requests for {recordsMap.Count} records ...");
			Parallel.ForEach(recordsToCompare,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				pair => createRequests.AddRange(GetFilteredRecords(pair.Key, pair.Value, RecordsFilterMode.UniqueInSource)
					.Select(e =>
						new CreateRequest
						{
							Target = pair.Value
								.FirstOrDefault(v => v.Record.LogicalName == e.LogicalName && v.Record.Id == e.Id)?.Record
								?? throw new Exception($"Couldn't find entity record => {e.LogicalName}:{e.Id}")
						})));

			log.Log($"Built {createRequests.Count} Create requests.");
			return createRequests;
		}

		private IEnumerable<UpdateRequest> CreateUpdateRequests()
		{
			var recordsToCompare = recordsMap.Values
				.Where(r => !r.IsCreate && r.IsUpdate)
				.GroupBy(r => r.Record.LogicalName)
				.ToDictionary(g => g.Key, g => g.Select(e => e));

			if (recordsToCompare.Any(r => r.Value.Any(r2 => r2.IsUseAlternateKeys)))
			{
				throw new Exception("A record exists that requires using Alternate Keys to sync;"
					+ " this is not supported with Update-only sync.");
			}

			var updateRequests = new List<UpdateRequest>();

			log.Log($"Creating Update requests for {recordsMap.Count} records ...");
			Parallel.ForEach(recordsToCompare,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				pair => updateRequests.AddRange(GetFilteredRecords(pair.Key, pair.Value, RecordsFilterMode.Common)
					.Select(e =>
						new UpdateRequest
						{
							Target = pair.Value
								.FirstOrDefault(v => v.Record.LogicalName == e.LogicalName && v.Record.Id == e.Id)?.Record
								?? throw new Exception($"Couldn't find entity record => {e.LogicalName}:{e.Id}")
						})));

			log.Log($"Built {updateRequests.Count} Update requests.");
			return updateRequests;
		}

		private void MapOldIdsToNewIds(IEnumerable<OrganizationRequest> requests, IEnumerable<ExecuteBulkResponse> responses)
		{
			var requestsArray = requests.ToArray();
			var responsesArray = responses.Select(r => r.Response).ToArray();
			var keys = idsMap.Keys.ToArray();

			for (var i = 0; i < responsesArray.Length; i++)
			{
				var response = responsesArray[i];
				idsMap[keys[i]] = GetIdFromRequestResponse(response) ?? GetIdFromRequestResponse(requestsArray[i]) ?? Guid.Empty;
			}
		}

		private void UpdateLookupRequestIds(List<UpdateRequest> dependencyRequests)
		{
			foreach (var record in dependencyRequests.Select(r => r.Target))
			{
				record.Id = idsMap[record.Id];

				foreach (var reference in record.Attributes.Values.OfType<EntityReference>())
				{
					reference.Id = idsMap[reference.Id];
				}
			}
		}

		private void ProcessObsoleteRelationsRemoval()
		{
			var relationsToProcess = relationsMap.Where(pair => pair.Key.IsDeleteObsolete || pair.Key.IsDisassociateObsolete);
			var disassociateRequests = new List<OrganizationRequest>();
			
			Parallel.ForEach(relationsToProcess,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = CrmService.Threads
				},
				relation =>
				{
					var relationDef = relation.Key;
					queries.TryGetValue(relationDef.QueryKey, out var fetchXml);

					if (string.IsNullOrEmpty(fetchXml))
					{
						log.LogWarning($"Can't find FetchXML for relation:"
							+ $" {relationDef.RelationshipInfo.SchemaName}:{relationDef.ParentReference.LogicalName}"
							+ $":{relationDef.ParentReference.Id}");
						return;
					}

					// add the necessary joins to load only related records and remove unnecessary attributes
					fetchXml = BuildExistingRelationsFetchXml(fetchXml, relationDef);
					log.Log($"Retrieving records using FetchXML ...");
					var existingRecordIds = service.RetrieveMultiple<Entity>(service.CloneQuery(new FetchExpression(fetchXml)))
						.Distinct(new EntityComparer()).Select(e => e.Id).ToList();
					log.Log($"Found {existingRecordIds.Count} records.");
					var obsoleteRecordIds = existingRecordIds.Except(relation.Value.Select(record => record.Id));

					disassociateRequests
						.AddRange(GetDisassociationRequests(relationDef, obsoleteRecordIds, relationDef.RelatedLogicalName));
				});

			log.Log($"Found {disassociateRequests.Count} obsolete records.");
			ExecuteRequests(disassociateRequests.ToList(), "Failed in obsolete relation removal step.");
		}

		private string BuildExistingRelationsFetchXml(string fetchXml, ExportedRelationDefinition relation)
		{
			var recordRef = relation.ParentReference;
			var relationMetadata = metadataHelper.GetRelationMetaData(recordRef.LogicalName, relation.RelationshipInfo.SchemaName);

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

			if (relationMetadata.Type == MetadataHelpers.RelationType.ManyToManyRelationships)
			{
				// add main entity ID field to avoid fetching all attributes
				stringBuilder.Append($"<attribute name='{relationMetadata.Entity1FieldName}' />");

				stringBuilder.Append($"<link-entity name='{relationMetadata.IntersectingEntityName}'"
					+ $" from='{relationMetadata.Entity1FieldName}' to='{relationMetadata.Entity1FieldName}' intersect='true'>");
				stringBuilder.Append($"<link-entity name='{relationMetadata.Entity2Name}'"
					+ $" from='{relationMetadata.Entity2FieldName}' to='{relationMetadata.Entity2FieldName}'>");

				stringBuilder.Append("<filter>");
				stringBuilder.Append(
					$"<condition attribute='{relationMetadata.Entity2FieldName}' operator='eq' value='{recordRef.Id}' />");
			}
			else
			{
				stringBuilder.Append($"<attribute name='{metadataHelper.GetIdFieldName(relationMetadata.Entity2Name)}' />");

				stringBuilder.Append($"<link-entity name='{relationMetadata.Entity1Name}'"
					+ $" from='{relationMetadata.Entity1FieldName}' to='{relationMetadata.Entity2FieldName}'>");

				stringBuilder.Append("<filter>");
				stringBuilder.Append(
					$"<condition attribute='{relationMetadata.Entity1FieldName}' operator='eq' value='{recordRef.Id}' />");
			}

			stringBuilder.Append("</filter>");

			stringBuilder.Append("</link-entity>");

			if (relationMetadata.Type == MetadataHelpers.RelationType.ManyToManyRelationships)
			{
				stringBuilder.Append("</link-entity>");
			}

			stringBuilder.Append("</entity></fetch>");

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

			return doc.OuterXml;
		}

		private IEnumerable<OrganizationRequest> GetDisassociationRequests(ExportedRelationDefinition relationDef,
			IEnumerable<Guid> obsoleteRecordIds, string relatedLogicalName)
		{
			var disassociateRequests = new List<OrganizationRequest>();

			if (relationDef.IsDeleteObsolete)
			{
				disassociateRequests.AddRange(obsoleteRecordIds
					.Select(id => new DeleteRequest { Target = new EntityReference(relatedLogicalName, id) }));
			}
			else if (relationDef.IsDisassociateObsolete)
			{
				var request =
					disassociateRequests.OfType<DisassociateRequest>()
						.FirstOrDefault(r => r.Target.Equals(relationDef.ParentReference)
							&& r.Relationship.Equals(relationDef.RelationshipInfo))
						?? new DisassociateRequest
						   {
							   Target = relationDef.ParentReference,
							   Relationship = relationDef.RelationshipInfo,
							   RelatedEntities = new EntityReferenceCollection()
						   };
				request.RelatedEntities.AddRange(obsoleteRecordIds
					.Select(id => new EntityReference(relatedLogicalName, id)));
			}
			else
			{
				log.LogWarning($"Relation flags for obsolete handling are all false for "
					+ $"'{relationDef.ParentReference.LogicalName}':'{relationDef.ParentReference.Id}'::"
					+ $"'{relationDef.RelationshipInfo.SchemaName}'");
			}

			return disassociateRequests;
		}

		private IEnumerable<AssociateRequest> CreateAssociateRequests()
		{
			log.Log($"Processing {relationsMap.Count} relationships ...");

			return relationsMap
				.Where(r => r.Value.Any())
				.Select(r =>
					new AssociateRequest
					{
						Target = r.Key.ParentReference,
						Relationship = r.Key.RelationshipInfo,
						RelatedEntities = new EntityReferenceCollection(r.Value)
					});
		}

		private void UpdateRelationRequestIds()
		{
			foreach (var relation in relationsMap)
			{
				relation.Key.ParentReference.Id = idsMap[relation.Key.ParentReference.Id];

				foreach (var reference in relation.Value)
				{
					reference.Id = idsMap[reference.Id];
				}
			}
		}

		private IDictionary<OrganizationRequest, ExecuteBulkResponse> ExecuteRequests(List<OrganizationRequest> requests,
			string message = null, Func<string, bool> textFilter = null)
		{
			log.Log($"Executing {requests.Count} Upsert requests ...");

			var bulkResponse = CrmHelpers.ExecuteBulk(service, requests, true, batchSize, false, BulkFinishHandler);
			ProcessBulkErrors(bulkResponse, textFilter ?? (faultMessage => !string.IsNullOrEmpty(faultMessage)), message);

			return bulkResponse;
		}

		private void ProcessBulkErrors(IDictionary<OrganizationRequest, ExecuteBulkResponse> bulkResponse,
			Func<string, bool> textFilter, string displayMessage = null)
		{
			var faults = bulkResponse.Where(response => textFilter(response.Value.FaultMessage))
				.Select(fault =>
						{
							if (fault.Key is UpsertRequest upsertRequest)
							{
								return $"Request: Upsert, Logical Name: {upsertRequest.Target.LogicalName}, " +
									$"Target: {upsertRequest.Target.Id}, " + fault.Value.FaultMessage;
							}

							if (fault.Key is UpdateRequest updateRequest)
							{
								return $"Request: Update, Logical Name: {updateRequest.Target.LogicalName}, " +
									$"Target: {updateRequest.Target.Id}, " + fault.Value.FaultMessage;
							}

							if (fault.Key is DeleteRequest deleteRequest)
							{
								return $"Request: Delete, Logical Name: {deleteRequest.Target.LogicalName}, " +
									$"Target: {deleteRequest.Target.Id}, " + fault.Value.FaultMessage;
							}

							if (fault.Key is AssociateRequest associateRequest)
							{
								var relatedString = associateRequest.RelatedEntities
									.Select(e => $"{{Logical Name: {e.LogicalName}, ID: {e.Id}}}")
									.Aggregate((e1, e2) => e1 + ", " + e2);

								return $"Request: Associate, Logical Name: {associateRequest.Target.LogicalName}, " +
									$"Target: {associateRequest.Target.Id}, " +
									$"Related: [{relatedString}]" + fault.Value.FaultMessage;
							}

							return fault.Value.FaultMessage;
						}).ToArray();

			if (faults.Any())
			{
				foreach (var fault in faults)
				{
					log.Log(fault, LogLevel.Error);
				}

				if (!string.IsNullOrEmpty(displayMessage))
				{
					throw new Exception($"{displayMessage} |=> {faults.LastOrDefault()}");
				}
			}
		}

		private Guid? GetIdFromRequestResponse<T>(T requestResponse)
		{
			if (requestResponse is OrganizationRequest)
			{
				return ((dynamic)requestResponse).Target.Id;
			}
			else if (requestResponse is CreateResponse)
			{
				return ((dynamic)requestResponse).id;
			}
			else
			{
				return null;
			}
		}
	}
}
