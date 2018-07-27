import { CrmConnectionService } from './services/crm-connection.service';
import { Component, OnInit } from '@angular/core';

@Component({
	selector: 'app-root',
	templateUrl: './app.component.html',
	styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit
{
	isConnected: boolean;
	url: string;
	isEasterEggShown = false;

	constructor(private crmConnectionService: CrmConnectionService) { }

	ngOnInit()
	{
		this.crmConnectionService.ConnectionEstablished
			.subscribe((response: { isConnected: boolean, url: string }) =>
			{
				this.isConnected = response.isConnected;
				this.url = response.url;
			});

		this.crmConnectionService.ConnectionError
			.subscribe(() =>
			{
				this.isConnected = false;
				this.url = null;
			});
	}
}
