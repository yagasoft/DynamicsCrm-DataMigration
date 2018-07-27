import { NotificationService } from './notification.service';
import { Observable } from 'rxjs/Observable';
import { HttpClientModule, HttpClient, HttpHeaders } from '@angular/common/http';
import { EventEmitter, Injectable, Output } from '@angular/core';
import { ConfigManagementService } from './config-management.service';

@Injectable()
export class CrmDataService
{
	// TODO
	private baseUrl: string = 'http://localhost:58001/api'

	constructor(
		private http: HttpClient,
		private configManager: ConfigManagementService,
		private notifyService: NotificationService)
	{ }

	export()
	{
		this.notifyService.notifyInfo(`Exporting data ...`, "Exporting!");
		return this.http.post(`${this.baseUrl}/export`, JSON.stringify(this.configManager.getConfig().toJSON()));
	}

	import(data: string)
	{
		this.notifyService.notifyInfo(`Importing data ...`, "Importing!");
		return this.http.post(`${this.baseUrl}/import`, data);
	}
}
