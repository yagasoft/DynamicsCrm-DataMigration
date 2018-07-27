import { FieldType } from './../../model/constants/field-type.enum';
import { ValueMappingViewModel } from './../../model/view-models/value-mapping.model';
import { ValueMapping, IValueMapping, ValuesMapping, IValuesMapping } from './../../model/configuration.model';
import { Helpers } from './../../libs/helpers.lib';
import { CrmMetadataService } from './../../services/crm-metadata.service';
import { NotificationService } from './../../services/notification.service';
import { Component, OnInit, EventEmitter, Output, Input } from '@angular/core';

declare var $: any;

@Component({
	selector: 'app-simple-mapping',
	templateUrl: './simple-mapping.component.html',
	styleUrls: ['./simple-mapping.component.scss']
})
export class SimpleMappingComponent implements OnInit
{
	@Input() logicalName: string;
	@Input() targetEntityName: string;
	@Input() mapping: ValueMapping[];
	@Output() finished = new EventEmitter<ValuesMapping>();

	// copies to save original
	modelTargetEntityName: string;
	mappingViewModel: ValueMappingViewModel[];

	results: { logicalName: string; displayName: string; fieldType: number }[] = [];
	filteredResults: string[] = [];
	placeholder = 'Type a field name';

	constructor(private notifyService: NotificationService, private crmMetadataService: CrmMetadataService) { }

	ngOnInit()
	{
		this.modelTargetEntityName = this.targetEntityName;

		const originalPlaceholder = this.placeholder;
		this.placeholder = "Loading names ...";

		// get all field names to display in the auto-complete list
		this.crmMetadataService.getEntityMetadata(this.logicalName).subscribe(
			(response: any) =>
			{
				const meta = response.FieldsMetaData;

				for (let i in meta)
				{
					const fieldMeta = meta[i];

					if (meta.hasOwnProperty(i))
					{
						this.results.push(
							{
								logicalName: fieldMeta.LogicalName,
								displayName: `${fieldMeta.DisplayName} (${fieldMeta.LogicalName})`,
								fieldType: fieldMeta.Type
							});
					}
				}

				this.mappingViewModel = this.mapping.map(this.mapToViewModel.bind(this));

				this.results.sort(
					function (a, b)
					{
						let a1 = a.displayName.toLowerCase();
						let b1 = b.displayName.toLowerCase();

						return a1 > b1 ? 1 : (a1 < b1 ? -1 : 0);
					});

				this.placeholder = originalPlaceholder;
			},
			response =>
			{
				console.error(response);
				this.notifyService.notifyError(Helpers.buildResponseErrorMessage(response), 'Failed to retrieve field list')
				this.placeholder = originalPlaceholder;
			});
	}

	add()
	{
		this.mappingViewModel.push(new ValueMappingViewModel());
	}

	remove(fieldMapping: ValueMappingViewModel)
	{
		this.mappingViewModel.splice(this.mappingViewModel.indexOf(fieldMapping), 1);
	}

	save()
	{
		// if all mappings have been removed
		if (!this.mappingViewModel.length)
		{
			this.finished.emit(null);
			return;
		}

		// make sure all required fields have correct value
		for (const fieldMapping of this.mappingViewModel)
		{
			if (!fieldMapping.sourceField || !fieldMapping.destinationField)
			{
				this.notifyService.notifyError('A required field is missing a value', 'Validation Issue');
				return;
			}
		}

		this.finished.emit(new ValuesMapping(<IValuesMapping>
			{
				targetEntityName: this.modelTargetEntityName,
				valueMappings: this.mappingViewModel.map(this.mapToMapping.bind(this))
			}));
	}

	cancel()
	{
		this.finished.emit(new ValuesMapping(<IValuesMapping>
			{
				targetEntityName: this.targetEntityName,
				valueMappings: this.mapping
			}));
	}

	search(event)
	{
		this.filteredResults =
			$.grep(this.results,
				function (a)
				{
					// filter such that the display or logical names contain the entered value
					return a.displayName.toLowerCase().indexOf(event.query.toLowerCase()) >= 0;
				});
	}

	mapToMapping(mappingViewModel: ValueMappingViewModel)
	{
		let mapping = JSON.parse(JSON.stringify(mappingViewModel));

		const sourceFieldname = mappingViewModel.sourceField.logicalName;
		const destinationFieldname = mappingViewModel.destinationField.logicalName;

		mapping.sourceField = sourceFieldname || mappingViewModel.sourceField;
		mapping.sourceFieldType = this.results.find((obj) => obj.logicalName === mapping.sourceField).fieldType || 15;
		mapping.destinationField = destinationFieldname || mappingViewModel.destinationField;

		if (mappingViewModel.destinationFieldType != null && typeof mappingViewModel.destinationFieldType !== 'undefined')
		{
			if (mappingViewModel.destinationFieldType.id != null && typeof mappingViewModel.destinationFieldType.id !== 'undefined')
			{
				mapping.destinationFieldType = mappingViewModel.destinationFieldType.id
			}
			else
			{
				mapping.destinationFieldType = mappingViewModel.destinationFieldType;
			}
		}
		else
		{
			var index = this.results.findIndex((obj) => obj.logicalName === mapping.destinationField);

			if (index >= 0)
			{
				mapping.destinationFieldType = this.results[index].fieldType || 15;
			}
			else
			{
				mapping.destinationFieldType = mapping.sourceFieldType;
			}
		}

		if (mapping.isIgnoreValues)
		{
			mapping.sourceValue = '';
			mapping.destinationValue = '';
		}

		return ValueMapping.fromJS(mapping);
	}

	mapToViewModel(mapping: ValueMapping)
	{
		let viewModel = JSON.parse(JSON.stringify(mapping));

		for (const key in FieldType)
		{
			if (FieldType.hasOwnProperty(key))
			{
				const element = FieldType[key];

				if (mapping.destinationFieldType === parseInt(key))
				{
					viewModel.destinationFieldType =
						{
							text: element,
							id: mapping.destinationFieldType
						}

					break;
				}
			}
		}

		const sourceField = <any>(this.results.find((obj) => obj.logicalName === mapping.sourceField) || mapping.sourceField);
		viewModel.sourceField =
			{
				logicalName: mapping.sourceField,
				displayName: sourceField.displayName || sourceField
			};

		const destinationField = <any>(this.results.find((obj) => obj.logicalName === mapping.destinationField) || mapping.destinationField);
		viewModel.destinationField =
			{
				logicalName: mapping.destinationField,
				displayName: destinationField.displayName || destinationField
			};

		return <ValueMappingViewModel>viewModel;
	}

	onChangeIsUseValues(index: number)
	{
		const mappingEntry = this.mappingViewModel[index];
		mappingEntry.isIgnoreValues = !mappingEntry.isIgnoreValues;
	}
}
