import { ExportOptions, ImportOptions } from './../model/configuration.model';
import { Configuration, Record, Relation } from '../model/configuration.model';
import { EventEmitter, Injectable } from '@angular/core';

@Injectable()
export class ConfigManagementService
{
	changedRecords = new EventEmitter<Record | Relation>();
	selectedNode = new EventEmitter<Record | Relation>();
	currentSelectedNode: Record | Relation;
	private config: Configuration;
	private flatNodes = {};

	constructor() { }

	getConfig(): Configuration
	{
		return this.config;
	}

	setConfig(config?: Configuration)
	{
		this.config = config || this.config || new Configuration();
		this.config.exportOptions = this.config.exportOptions
			|| new ExportOptions(<ExportOptions>
				{
					isCompressData: true,
					isExcludeOwner: true
				});
		this.config.importOptions = this.config.importOptions
			|| new ImportOptions(<ImportOptions>
				{
					bulkSize: 500
				});
		this.config.records = this.config.records || [];
		this.setRecords(this.config.records);

		this.currentSelectedNode = this.config.records[0];
		this.changedRecords.emit(this.currentSelectedNode);
	}

	resetConfig()
	{
		this.config = new Configuration();
		this.config.exportOptions =
			new ExportOptions(<ExportOptions>
				{
					isCompressData: true,
					isExcludeOwner: true
				});
		this.config.importOptions =
			new ImportOptions(<ImportOptions>
				{
					bulkSize: 500
				});
		this.config.records = [];

		this.currentSelectedNode = null;
		this.changedRecords.emit();
		this.selectedNode.emit();
	}

	private setRecords(records: Record[])
	{
		for (let record of records)
		{
			this.addFlatNode(record);

			if (record.relations)
			{
				this.parseRelations(record.relations, record.defId);
			}
		}
	}

	private addFlatNode(node: Record | Relation, parentDefId?: string)
	{
		this.flatNodes[node.defId] =
			{
				node: node,
				parentDefId: parentDefId
			};
	}

	getRecords(): Record[]
	{
		if (!this.config || !this.config.records)
		{
			return [];
		}

		return this.config.records;
	}

	getNode(defId: string): Record | Relation
	{
		var flatNode = this.flatNodes[defId];
		return flatNode ? flatNode.node : null;
	}

	getParent(defId: string): Record | Relation
	{
		var flatNode = this.flatNodes[defId];
		return flatNode ? this.getNode(flatNode.parentDefId) : null;
	}

	addNode(node: Relation | Record, parentDefId?: string)
	{
		if (!this.config || !this.config.records)
		{
			return;
		}

		let parentNode = this.getNode(parentDefId);

		if (parentNode)
		{
			let relations;

			if (parentNode instanceof Record)
			{
				relations = parentNode.relations = parentNode.relations || [];
			} else
			{
				let relationNode = <Relation>node;
				relations = relationNode.entityDefinition.relations =
					relationNode.entityDefinition.relations || [];
			}

			let relation = <Relation>node;
			relations.push(relation);
			this.parseRelations([relation], parentNode.defId);
		} else
		{
			if (node instanceof Record)
			{
				let record = <Record>node;
				this.config.records.push(record);
				this.addFlatNode(record, parentDefId);

				if (record.relations)
				{
					this.parseRelations(record.relations, record.defId);
				}
			}
		}

		this.currentSelectedNode = node;
		this.changedRecords.emit(this.currentSelectedNode);
	}

	private parseRelations(relations: Relation[], parentDefId?: string)
	{
		for (let relation of relations)
		{
			this.addFlatNode(relation, parentDefId);
			this.addFlatNode(relation.entityDefinition, relation.defId);

			if (relation.entityDefinition.relations)
			{
				this.parseRelations(relation.entityDefinition.relations,
					relation.entityDefinition.defId);
			}
		}
	}

	removeNode(defId: string)
	{
		var parent = this.getParent(defId);

		if (parent instanceof Record)
		{
			var index = parent.relations.findIndex(r => r.defId === defId);

			if (index >= 0)
			{
				parent.relations.splice(index, 1);
			}

			this.currentSelectedNode = this.getParent(parent.defId);
		}
		else
		{
			var records = this.getRecords();
			var index = records.findIndex(r => r.defId === defId);

			if (index >= 0)
			{
				records.splice(index, 1);
				this.currentSelectedNode = records[0];
			}
		}

		this.changedRecords.emit(this.currentSelectedNode);
	}

	selectNode(defId: string)
	{
		this.currentSelectedNode = this.getNode(defId);
		this.selectedNode.emit(this.currentSelectedNode);
	}
}
