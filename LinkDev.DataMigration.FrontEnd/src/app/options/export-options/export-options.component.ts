import { ConfigManagementService } from './../../services/config-management.service';
import { ExportOptions } from './../../model/configuration.model';
import { FormGroup, FormBuilder } from '@angular/forms';
import { Component, OnInit, SimpleChanges, OnDestroy } from '@angular/core';
import { Record } from '../../model/configuration.model';

@Component({
	selector: 'app-export-options',
	templateUrl: './export-options.component.html',
	styleUrls: ['./export-options.component.scss']
})
export class ExportOptionsComponent implements OnInit, OnDestroy
{
	form: FormGroup;
	options: ExportOptions;
	changedRecordsSub;

	constructor(private configManager: ConfigManagementService, private formBuilder: FormBuilder)
	{
		this.createForm();
	 }

	ngOnInit()
	{
		this.changedRecordsSub = this.configManager.changedRecords
			.subscribe(() =>
			{
				const config = this.configManager.getConfig();
				this.updateForm(config ? config.exportOptions : null);
			});		
	}

	ngOnDestroy()
	{
		if (this.changedRecordsSub)
		{
			this.changedRecordsSub.unsubscribe();
		}	
	}

	private createForm()
	{
		this.form = this.formBuilder.group(this.getViewOptions());
	}

	private getViewOptions(options?: ExportOptions): ExportOptions
	{
		options = options || new ExportOptions();

		return new ExportOptions(<ExportOptions>
			{
				isCompressData: options.isCompressData || false,
				isExcludeOwner: options.isExcludeOwner || false
			});
	}

	private updateForm(options?: ExportOptions)
	{
		this.options = options;
		this.resetForm();
	}

	private resetForm()
	{
		this.form.reset(this.getViewOptions(this.options));
	}

	save()
	{
		let viewOptions = this.form.value;

		for (let p in viewOptions)
		{
			if (viewOptions.hasOwnProperty(p))
			{
				this.options[p] = viewOptions[p];
			}
		}

		this.resetForm();
	}

	cancel()
	{
		this.resetForm();
	}
}
