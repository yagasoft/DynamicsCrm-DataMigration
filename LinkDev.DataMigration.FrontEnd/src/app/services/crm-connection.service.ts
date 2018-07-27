import { NotificationService } from './notification.service';
import { Observable } from 'rxjs/Rx';
import { HttpClientModule, HttpClient } from '@angular/common/http';
import { EventEmitter, Injectable, Output } from '@angular/core';

@Injectable()
export class CrmConnectionService
{
	@Output() ConnectionEstablished = new EventEmitter<{ isConnected: boolean, url: string }>();
	@Output() ConnectionError = new EventEmitter<string>();
	isConnected: boolean;
	connectionString: string;

	// TODO
	private baseUrl: string = 'http://localhost:58001/api/crmservice'

	constructor(
		private http: HttpClient,
		private notifyService: NotificationService)
	{ }

	connectToCrm(connectionString: string, maxThreadCount: number = 1)
	{
		this.ConnectionEstablished.emit({ isConnected: false, url: '' });

		this.notifyService.notifyInfo(`Connecting to ... "${connectionString}"`, "Connecting!");

		this.http.post(this.baseUrl,
			{
				ConnectionString: connectionString,
				MaxThreadCount: maxThreadCount
			}).subscribe(
			(response: string) =>
			{
				this.isConnected = true;
				this.connectionString = response;
				const connStringArray = response.split(';');
				let url = '';

				// search for the URL in the connection string
				for (const connStringPart of connStringArray)
				{
					const parts = connStringPart.split('=');

					if (parts[0].toLowerCase() == 'url' && parts.length > 1)
					{
						url = parts[1];
						break;
					}
				}

				this.ConnectionEstablished.emit({ isConnected: true, url: url })
			},
			(response: string) => this.ConnectionError.emit(response));
	}
}
