import { Injectable } from '@angular/core';
import { NotificationsService } from 'angular2-notifications';
import { AlertsService } from '@jaspero/ng2-alerts';
import { ResolveEmit } from '@jaspero/ng2-confirmations/src/interfaces/resolve-emit';
import { ConfirmationService } from 'primeng/primeng';

@Injectable()
export class NotificationService
{
	constructor(private notificationService: NotificationsService, private alertService: AlertsService, private confirmationService: ConfirmationService) { }

	notifyError(message: string, title?: string)
	{
		this.notify(title ? title : 'Error', message, 'error');
	}

	notifyWarning(message: string, title?: string, timeOut?: number)
	{
		this.notify(title ? title : 'Warning', message, 'warn');
	}

	notifyInfo(message: string, title?: string, timeOut?: number)
	{
		this.notify(title ? title : 'Info', message, 'info');
	}

	notifySuccess(message: string, title?: string, timeOut?: number)
	{
		this.notify(title ? title : 'Success', message, 'success', timeOut);
	}

	confirm(title: string, message: string, callback: (boolean) => void)
	{
		this.confirmationService.confirm({
			header: title,
			message: message,
			accept: () => callback(true),
			reject: () => callback(false)
		});
	}

	private notify(title: string, message: string, level: string, timeOut = 10000)
	{
		if (level === 'error')
		{
			this.alertService.create(level, `${title} => ${message}`)
		}
		else
		{
			this.notificationService.create(title, message, level, { timeOut: timeOut });
		}
	}
}
