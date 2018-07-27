import { Helpers } from './../../libs/helpers.lib';
import { NotificationService } from './../../services/notification.service';
import { ConfigManagementService } from '../../services/config-management.service';
import { Record } from '../../model/configuration.model';
import generateId from '../../libs/random.lib';
import { Component, ElementRef, OnInit, ViewChild, Output, EventEmitter } from '@angular/core';
import { CrmMetadataService } from '../../services/crm-metadata.service';
import { FormsModule } from "@angular/forms";

declare var $: any;

@Component({
	selector: "app-entity-selector",
	templateUrl: "./entity-selector.component.html",
	styleUrls: ["./entity-selector.component.scss"]
})
export class EntitySelectorComponent implements OnInit
{
	@ViewChild("entityTextBox") entityTextBox: ElementRef;
	@ViewChild("entityTextBoxList") entityTextBoxList: ElementRef;
	@Output() entityAdd = new EventEmitter<string>();

	entity: { logicalName: string; displayName: string };
	results: { logicalName: string; displayName: string }[] = [];
	filteredResults: string[] = [];
	placeholder = 'Type an entity name';

	constructor(
		private crmMetadataService: CrmMetadataService,
		private configManager: ConfigManagementService,
		private notifyService: NotificationService
	) { }

	ngOnInit()
	{
		const originalPlaceholder = this.placeholder;
		this.placeholder = "Loading names ...";

		this.crmMetadataService.getEntityNames().subscribe(
			response =>
			{
				for (let e in response)
				{
					if (response.hasOwnProperty(e))
					{
						this.results.push({ logicalName: e, displayName: `${response[e]} (${e})` });
					}
				}

				this.results.sort(
					function (a, b)
					{
						let a1 = a.displayName.toLowerCase();
						let b1 = b.displayName.toLowerCase();

						return a1 > b1 ? 1 : (a1 < b1 ? -1 : 0);
					});

				this.entity = null;
				this.placeholder = originalPlaceholder;
			},
			response =>
			{
				console.error(response);
				this.notifyService.notifyError(Helpers.buildResponseErrorMessage(response),
					'Failed to retrieve entity list')
				this.placeholder = originalPlaceholder;
			});
	}

	addEntity()
	{
		if (!this.entity)
		{
			return;
		}

		let newRecord = new Record();
		newRecord.defId = `DM_${generateId(20)}`;
		newRecord.label = this.entity.displayName;
		newRecord.logicalName = this.entity.logicalName;
		newRecord.isCreateNewRecords = true;
		newRecord.isUpdateExistingRecords = true;
		this.configManager.addNode(newRecord);
		this.entity = null;
	}

	search(event)
	{
		this.filteredResults =
			$.grep(this.results,
				function (a)
				{
					return a.displayName.toLowerCase().indexOf(event.query.toLowerCase()) >= 0;
				});
	}
}
