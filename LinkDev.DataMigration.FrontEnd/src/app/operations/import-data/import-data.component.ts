import { ConfigManagementService } from './../../services/config-management.service';
import { Helpers } from './../../libs/helpers.lib';
import { CrmDataService } from './../../services/crm-data.service';
import { Message } from 'primeng/components/common/api';
import { MessageService } from 'primeng/components/common/messageservice';
import { Component, OnInit } from '@angular/core';
import { NotificationService } from '../../services/notification.service';

@Component({
	selector: 'app-import-data',
	templateUrl: './import-data.component.html',
	styleUrls: ['./import-data.component.scss']
})
export class ImportDataComponent implements OnInit
{
	isImporting = false;

	constructor(private dataService: CrmDataService,
		private notifyService: NotificationService,
		private configManager: ConfigManagementService)
	{
		if (!this.configManager.getConfig())
		{
			this.configManager.setConfig();
		}
	}

	ngOnInit()
	{

	}

	importData(target: HTMLInputElement)
	{
		if (!target || !target.value)
		{
			return;
		}

		this.isImporting = true;

		const reader = new FileReader();
		const component = this;

		reader.onload =
			function ()
			{
				try
				{
					const result = reader.result;
					component.dataService.import(result + '<|||>' +
						JSON.stringify(component.configManager.getConfig().importOptions.toJSON()))
						.subscribe((response: any) =>
						{
							target.value = null;
							component.isImporting = false;
							component.notifyService.notifySuccess("Successfully imported data.");
						},
						response =>
						{
							console.error(response);
							component.isImporting = false;
							component.notifyService.notifyError(Helpers.buildResponseErrorMessage(response),
								'Failed to import data')
						});;
				}
				catch (e)
				{
					console.error(e);
					component.isImporting = false;
					component.notifyService.notifyError(e.message, 'Failed to load file');
				}
			};

		reader.readAsText(target.files[0]);
	}

}
