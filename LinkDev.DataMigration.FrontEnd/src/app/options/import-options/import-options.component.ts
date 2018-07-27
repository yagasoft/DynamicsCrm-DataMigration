import { OnDestroy } from '@angular/core';
import { ConfigManagementService } from './../../services/config-management.service';
import { ImportOptions } from './../../model/configuration.model';
import { FormGroup, FormBuilder } from '@angular/forms';
import { Component, OnInit } from '@angular/core';

@Component({
	selector: 'app-import-options',
	templateUrl: './import-options.component.html',
	styleUrls: ['./import-options.component.scss']
})
export class ImportOptionsComponent implements OnInit, OnDestroy
{
	form: FormGroup;
	options: ImportOptions;
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
				this.updateForm(config ? config.importOptions : null);
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

	private getViewOptions(options?: ImportOptions): ImportOptions
	{
		options = options || new ImportOptions();

		return new ImportOptions(<ImportOptions>
			{
				bulkSize: options.bulkSize || 0
			});
	}

	private updateForm(options?: ImportOptions)
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
