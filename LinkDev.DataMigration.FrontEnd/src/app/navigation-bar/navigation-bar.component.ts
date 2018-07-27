import { StateService } from './../services/state.service';
import { Helpers } from './../libs/helpers.lib';
import { NotificationService } from './../services/notification.service';
import { Configuration } from './../model/configuration.model';
import { ConfigManagementService } from './../services/config-management.service';
import { Component, OnInit } from '@angular/core';
import { saveAs } from 'file-saver/FileSaver';
import { CrmDataService } from '../services/crm-data.service';

@Component({
	selector: 'app-navigation-bar',
	templateUrl: './navigation-bar.component.html',
	styleUrls: ['./navigation-bar.component.scss']
})
export class NavigationBarComponent implements OnInit
{
	mode;

	constructor(
		private configManager: ConfigManagementService,
		private dataService: CrmDataService,
		private notifyService: NotificationService)
	{ }

	ngOnInit()
	{ }

	newConfig()
	{
		if (this.configManager.getRecords().length)
		{
			this.notifyService.confirm('New Configuration', 'Are you sure you want to abandon the current configuration?',
				isConfirmed =>
				{
					if (isConfirmed)
					{
						this.configManager.resetConfig();
					}
				});
		}
		else
		{
			this.configManager.resetConfig();
		}
	}

	loadConfig(event: Event)
	{
		const target: HTMLInputElement = event.target as HTMLInputElement;

		if (!target || !target.value)
		{
			return;
		}

		const reader = new FileReader();
		const component = this;

		reader.onload =
			function ()
			{
				try
				{
					const result = reader.result;
					const json = JSON.parse(result);
					const config = Configuration.fromJS(json);
					component.configManager.setConfig(config);
					component.notifyService.notifySuccess(`Successfully loaded config from "${target.value}"`, "Loaded!");
				}
				catch (e)
				{
					console.error(e);
					component.notifyService.notifyError(e.message, 'Failed to load file');
				}
				finally
				{
					target.value = null;
				}
			};

		reader.readAsText(target.files[0]);
	}

	onLoadConfigClick($event)
	{
		if (this.configManager.getRecords().length)
		{
			this.notifyService.confirm('Load Configuration', 'Are you sure you want to abandon the current configuration?',
				isConfirmed =>
				{
					if (isConfirmed)
					{
						$event.click();
					}
				});
		}
		else
		{
			$event.click();
		}

		return false;
	}

	saveConfig()
	{
		const blob = new Blob([JSON.stringify(this.configManager.getConfig().toJSON())], { type: 'text/plain;charset=utf-8' });
		saveAs(blob, 'dm-configuration.json');
		this.notifyService.notifySuccess(`Successfully saved config.`, "Saved!");
	}

	exportData()
	{
		const component = this;

		this.dataService.export()
			.subscribe((response: any) =>
			{
				let isCompress: any = this.configManager.getConfig().exportOptions.isCompressData;
				isCompress = isCompress === true ? 'TRUE' : 'FALSE';

				const blob = new Blob([response + '<|||>' + isCompress],
					{ type: 'text/plain;charset=utf-8' });
				saveAs(blob, 'dm-data.dat');
				component.notifyService.notifySuccess(`Successfully exported data.`, "Exported!");
			},
			response =>
			{
				console.error(response);
				component.notifyService.notifyError(` => ${Helpers.buildResponseErrorMessage(response)}`,
					'Failed to export data');
			});
	}
}
