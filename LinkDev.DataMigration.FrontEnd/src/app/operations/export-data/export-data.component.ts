import { ActivatedRoute, Router, Params } from '@angular/router';
import { ConfigManagementService } from '../../services/config-management.service';
import
{
	Configuration,
	ExportOptions,
	ImportOptions,
	Record,
	Relation,
} from '../../model/configuration.model';
import { Component, OnInit, OnDestroy } from '@angular/core';

@Component({
	selector: "app-export-data",
	templateUrl: "./export-data.component.html",
	styleUrls: ["./export-data.component.scss"]
})
export class ExportDataComponent implements OnInit, OnDestroy
{
	defId: string;
	selectedNodeSub;

	constructor(
		private configManager: ConfigManagementService,
		private router: Router,
		private route: ActivatedRoute
	)
	{
		this.configManager.setConfig();
		this.selectedNodeSub = this.configManager.selectedNode.subscribe((node: Record | Relation) => this.updateDetails(node));
	}

	ngOnInit()
	{
		this.route.paramMap
			.subscribe((params: Params) =>
			{
				this.defId = params.params['defId'];
			});
	}

	ngOnDestroy()
	{
		if (this.selectedNodeSub)
		{
			this.selectedNodeSub.unsubscribe();
		}	
	}

	updateDetails(node: Record | Relation)
	{
		if (node && this.defId == node.defId)
		{
			return;
		}

		var path = '';

		if (this.defId)
		{
			path += '../';
		}

		if (node && node.defId)
		{
			path += node.defId;
		}

		if (path)
		{
			this.router.navigate([path], { relativeTo: this.route });
		}
	}
}
