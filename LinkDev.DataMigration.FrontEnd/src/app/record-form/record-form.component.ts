import { ValueMappingViewModel } from './../model/view-models/value-mapping.model';
import { NotificationService } from './../services/notification.service';
import { CrmMetadataService } from './../services/crm-metadata.service';
import { Relation, ValueMapping, ValuesMapping, IValuesMapping } from './../model/configuration.model';
import { Input, ElementRef, ViewChild } from '@angular/core';
import { Record } from '../model/configuration.model';
import { ConfigManagementService } from '../services/config-management.service';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { Component, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { FormControl, FormBuilder, FormGroup } from '@angular/forms';
import { getRandomString } from 'selenium-webdriver/safari';
import generateId from '../libs/random.lib';

@Component({
	selector: 'app-record-form',
	templateUrl: './record-form.component.html',
	styleUrls: ['./record-form.component.scss']
})
export class RecordFormComponent implements OnChanges, OnInit
{
	@Input() defId: string;
	relation: Relation;
	record: Record;
	recordForm: FormGroup;
	relationForm: FormGroup;
	recordOptionsForm: FormGroup;
	relationOptionsForm: FormGroup;
	relations: { schemaName: string, type: number, targetEntity: string }[] = [];

	mapping: ValuesMapping;
	isValueMappingShown = false;

	constructor(
		private metadataService: CrmMetadataService,
		private configManager: ConfigManagementService,
		private notifyService: NotificationService,
		private router: Router,
		private route: ActivatedRoute,
		private formBuilder: FormBuilder
	)
	{
		this.createForm();
	}

	ngOnInit()
	{
		this.updateForm(this.configManager.getNode(this.defId));
	}

	ngOnChanges(changes: SimpleChanges)
	{
		this.updateForm(this.configManager.getNode(changes.defId.currentValue));
	}

	private createForm()
	{
		this.recordForm = this.formBuilder.group(this.getViewRecord());
		this.relationForm = this.formBuilder.group(this.getViewRelation());
		this.recordOptionsForm = this.formBuilder.group(this.getViewRecordOptions());
		this.relationOptionsForm = this.formBuilder.group(this.getViewRelationOptions());
	}

	private updateForm(record: Record | Relation)
	{
		if (record instanceof Relation)
		{
			this.relation = record;
			this.record = record.entityDefinition;
		}
		else
		{
			this.record = record;
			this.relation = null;
		}
		
		if (this.record)
		{
			this.mapping = this.record.valuesMapping;
		}

		if (this.record)
		{
			this.record.fetchXml = this.record.fetchXml
				||
				`<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true" no-lock="true">
  <entity name="${this.record.logicalName}">
    
  </entity>
</fetch>`
		}

		this.resetForm();
		this.loadRelations();
	}

	private loadRelations()
	{
		if (!this.record)
		{
			return;
		}

		this.relations = [];

		this.metadataService.getEntityMetadata(this.record.logicalName)
			.subscribe((response: any) =>
			{
				var relations = response.RelationsMetaData;

				if (!relations)
				{
					return;
				}

				relations.sort(
					function (a, b)
					{
						return ((a.Type > b.Type) ? 1 : ((a.Type < b.Type) ? -1 : 0))
							|| ((a.SchemaName.toLowerCase() > b.SchemaName.toLowerCase()) ? 1 : ((b.SchemaName.toLowerCase() > a.SchemaName.toLowerCase()) ? -1 : 0));
					});

				for (let e in relations)
				{
					this.relations.push(
						{
							schemaName: relations[e].SchemaName,
							type: relations[e].Type,
							targetEntity: relations[e].Entity2Name
						});
				}
			},
			response =>
			{
				console.error(response);
			});
	}

	private getViewRecord(record?: Record): Record
	{
		record = record || new Record();

		return new Record(<Record>
			{
				label: record.label || '',
				logicalName: record.logicalName || '',
				fetchXml: record.fetchXml || ''
			});
	}

	private getViewRecordOptions(record?: Record): Record
	{
		record = record || new Record();

		return new Record(<Record>
			{
				isDeleteObsoleteRecords: record.isDeleteObsoleteRecords || false,
				isUseAlternateKeysForRecord: record.isUseAlternateKeysForRecord || false,
				isUseAlternateKeysForLookups: record.isUseAlternateKeysForLookups || false,
				isClearInvalidLookups: record.isClearInvalidLookups || false,
				isCreateNewRecords: record.isCreateNewRecords || false,
				isUpdateExistingRecords: record.isUpdateExistingRecords || false
			});
	}

	private getViewRelation(relation?: Relation): any
	{
		relation = relation || new Relation();

		return {
			label: relation.entityDefinition.label || '',
			schemaName: relation.schemaName || '',
		};
	}

	private getViewRelationOptions(relation?: Relation): any
	{
		relation = relation || new Relation();

		return {
			isDeleteObsoleteRelations: relation.isDeleteObsoleteRelations || false,
			isDisassociateObsoleteRelations: relation.isDisassociateObsoleteRelations || false
		};
	}

	private resetForm()
	{
		this.recordForm.reset(this.getViewRecord(this.record));
		this.relationForm.reset(this.getViewRelation(this.relation));
		this.recordOptionsForm.reset(this.getViewRecordOptions(this.record));
		this.relationOptionsForm.reset(this.getViewRelationOptions(this.relation));
	}

	delete()
	{
		this.notifyService.confirm('Node Deletion', 'Are you sure you want to delete this node?',
			isConfirmed =>
			{
				if (isConfirmed)
				{
					this.configManager.removeNode(this.relation ? this.relation.defId : this.record.defId);
				}
			});
	}

	save()
	{
		let viewRecord = this.recordForm.value;

		for (let p in viewRecord)
		{
			if (viewRecord.hasOwnProperty(p))
			{
				this.record[p] = viewRecord[p];
			}
		}

		let viewRecordOptions = this.recordOptionsForm.value;

		for (let p in viewRecordOptions)
		{
			if (viewRecordOptions.hasOwnProperty(p))
			{
				this.record[p] = viewRecordOptions[p];
			}
		}

		if (this.relation)
		{
			let viewRelation = this.relationForm.value;

			for (let p in viewRelation)
			{
				if (viewRelation.hasOwnProperty(p))
				{
					this.relation[p] = viewRelation[p];
				}
			}

			let viewRelationOptions = this.relationOptionsForm.value;

			for (let p in viewRelationOptions)
			{
				if (viewRelationOptions.hasOwnProperty(p))
				{
					this.relation[p] = viewRelationOptions[p];
				}
			}
		}

		this.resetForm();
		this.configManager.changedRecords.emit(this.record);
	}

	cancel()
	{
		this.resetForm();
	}

	addRelation(relation: { schemaName: string, type: number, targetEntity: string })
	{
		this.configManager.addNode(
			new Relation(<Relation>
				{
					defId: `DM_${generateId(20)}`,
					schemaName: relation.schemaName,
					relationType: relation.type,
					entityDefinition: new Record(<Record>
						{
							defId: `DM_${generateId(20)}`,
							label: relation.schemaName,
							logicalName: relation.targetEntity
						})
				}),
			this.record.defId);
	}

	mapValues()
	{
		this.mapping = this.mapping
			||
			new ValuesMapping(<IValuesMapping>
				{
					valueMappings: []
				});
		this.isValueMappingShown = true;
	}

	onMappingFinished(newMapping: ValuesMapping)
	{
		this.mapping = newMapping;
		this.isValueMappingShown = false;
		this.record.valuesMapping = this.mapping;
	}
}
