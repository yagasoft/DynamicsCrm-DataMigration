import { ConfigManagementService } from './../services/config-management.service';
import { ConnectionViewModel } from './../model/view-models/connection.model';
import { FormGroup, FormBuilder, Validators, FormControl } from '@angular/forms';
import { Helpers } from './../libs/helpers.lib';
import { NotificationService } from './../services/notification.service';
import { CrmConnectionService } from '../services/crm-connection.service';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { SelectItem } from 'primeng/primeng';

@Component({
	selector: 'app-crm-connect',
	templateUrl: './crm-connect.component.html',
	styleUrls: ['./crm-connect.component.scss']
})
export class CrmConnectComponent implements OnDestroy
{
	form: FormGroup;
	connectionErrorMessage: string;
	authType = 'AD';

	estabishConnSub;
	connErrorSub;

	constructor(private formBuilder: FormBuilder,
		private crmConnectionService: CrmConnectionService,
		private router: Router,
		private notifyService: NotificationService)
	{
		this.createForm();
	}

	private createForm()
	{
		const viewModel = new ConnectionViewModel();
		let formModel = {};

		for (const p in viewModel)
		{
			if (viewModel.hasOwnProperty(p))
			{
				formModel[p] = p === 'Url' ? [viewModel[p], Validators.required] : [viewModel[p]];
			}
		}
		
		this.form = this.formBuilder.group(formModel);

		// this.connectToCrm();
	}

	connectToCrm()
	{
		const connection = this.form.value
		let connectionString = '';

		for (const p in connection)
		{
			if (p !== 'Threads' && connection.hasOwnProperty(p))
			{
				if (connection[p])
				{
					connectionString += p + '=' + connection[p] + ';';
				}
			}
		}
		//const connectionString = "AuthType=AD;Url=http://192.168.137.229/GenericSolution;Username=administrator;Password=a;Domain=yagasoft1;";
		
		this.form.disable();
		this.connectionErrorMessage = null;
		const component = this;

		this.estabishConnSub = this.crmConnectionService.ConnectionEstablished
			.subscribe((response: { isConnected: boolean, url: string }) =>
			{
				if (response.isConnected)
				{
					this.router.navigate(['operations']);
					this.form.enable();
					component.notifyService.notifySuccess(`Successfully connected to "${connectionString}"`, "Connected!");
				}
			});

		this.connErrorSub = this.crmConnectionService.ConnectionError
			.subscribe((response) =>
			{
				component.notifyService.notifyError(Helpers.buildResponseErrorMessage(response), 'Failed to connect');
				this.form.enable();
			});

		this.crmConnectionService.connectToCrm(connectionString, connection.Threads);
	}

	ngOnDestroy()
	{
		if (this.estabishConnSub)
		{
			this.estabishConnSub.unsubscribe();
		}

		if (this.connErrorSub)
		{
			this.connErrorSub.unsubscribe();
		}	
	}
}
