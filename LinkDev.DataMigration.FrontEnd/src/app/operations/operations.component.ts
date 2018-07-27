import { NotificationService } from './../services/notification.service';
import { saveAs } from 'file-saver/FileSaver';
import { SignalrService } from './../services/signalr.service';
import { CrmConnectionService } from './../services/crm-connection.service';
import { ConfigManagementService } from './../services/config-management.service';
import { Component, OnInit, ViewChild, ElementRef, Renderer2 } from '@angular/core';
import { ActivatedRoute, Params, Router } from '@angular/router';

declare var $: any;

@Component({
	selector: 'app-operations',
	templateUrl: './operations.component.html',
	styleUrls: ['./operations.component.scss']
})
export class OperationsComponent implements OnInit
{
	@ViewChild('logElement') logElement: ElementRef;
	isLogShown = false;

	constructor(private router: Router,
		private connectionService: CrmConnectionService,
		private notifyService: NotificationService,
		private signalrService: SignalrService,
		private renderer: Renderer2) { }

	ngOnInit()
	{
		if (!this.connectionService.isConnected)
		{
			// this.router.navigate(['/']);
			// return;
		}

		this.signalrService.logEntryAdded
			.subscribe((event: { entry: string, level: string }) =>
			{
				const native = this.logElement.nativeElement;
				const isAtBottom = (native.scrollTop + native.clientHeight) === native.scrollHeight;
				const htmlEntry = this.renderer.createElement('div');
				this.renderer.appendChild(htmlEntry, this.renderer.createText(event.entry));
				this.renderer.addClass(htmlEntry, event.level.toLowerCase());
				this.renderer.appendChild(native, htmlEntry);

				if (isAtBottom)
				{
					native.scrollTop = native.scrollHeight;
				}
			});
		
		$("#log-outer-container")
			.resizable(
			{
				animate: true,
				animateDuration: "fast",
				autoHide: true,
				ghost: true,
				handles: "n",
				minHeight: 50
			});
	}

	toggleLog()
	{
		this.isLogShown = !this.isLogShown;
	}

	clearLog()
	{
		this.logElement.nativeElement.innerHTML = '';
	}

	downloadLog()
	{
		const blob = new Blob([`<html><body>${this.logElement.nativeElement.innerHTML}</html></html>`], { type: 'text/plain;charset=utf-8' });
		saveAs(blob, 'dm-log.html');
		this.notifyService.notifySuccess(`Successfully saved log.`, "Saved!");
	}
}
