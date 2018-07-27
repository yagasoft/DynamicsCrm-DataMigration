import { ConfigManagementService } from '../services/config-management.service';
import { selector } from 'rxjs/operator/publish';
import { EntitiesTreeTreeComponent } from './entities-tree-tree/entities-tree-tree.component';
import { Component, OnInit, Input, Output, EventEmitter } from "@angular/core";
import { Record, IRecord, IRelation } from '../model/configuration.model';
import generateId from '../libs/random.lib';

@Component({
	selector: "app-entities-tree",
	templateUrl: "./entities-tree.component.html",
	styleUrls: ["./entities-tree.component.scss"]
})
export class EntitiesTreeComponent
{
	constructor(private configManager: ConfigManagementService) { }
}
