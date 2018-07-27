import { ConfigManagementService } from '../../services/config-management.service';
import { Record, Relation } from './../../model/configuration.model';
import { Component, OnInit, Input, Output, AfterViewInit, EventEmitter, OnDestroy } from "@angular/core";
declare var $: any;

@Component({
	selector: "app-entities-tree-tree",
	templateUrl: "./entities-tree-tree.component.html",
	styleUrls: ["./entities-tree-tree.component.scss"]
})
export class EntitiesTreeTreeComponent implements AfterViewInit, OnDestroy
{
	isTreeInitialised = false;
	changedRecordsSub;

	constructor(private configManager: ConfigManagementService) { }

	ngAfterViewInit()
	{
		this.updateTree();
		this.changedRecordsSub = this.configManager.changedRecords
			.subscribe((node: Record | Relation) => this.updateTree());
	}

	ngOnDestroy()
	{
		if (this.changedRecordsSub)
		{
			this.changedRecordsSub.unsubscribe();
		}	
	}

	private updateTree()
	{
		let treeElement = $('#records-tree');

		if (this.isTreeInitialised)
		{
			const treeInstance = treeElement.jstree();

			if (treeInstance && typeof treeInstance.load_all === 'function')
			{
				treeInstance.load_node({ id: '#' });
				return;
			}
		}

		const configManager = this.configManager;
		const component = this;

		const selectNode =
			function ()
			{
				const instance = treeElement.jstree();
				instance.open_all();

				if (configManager.currentSelectedNode)
				{
					instance.select_node(configManager.currentSelectedNode.defId);
				}
				else
				{
					instance.deselect_all();
				}	
			};

		const fillTree =
			function (node, callback)		
			{
				if (node.id !== '#')
				{
					return;
				}

				let records = configManager.getRecords();

				if (!records)
				{
					callback([]);
					return;
				}

				let nodes = [];

				for (let record of records)
				{
					let node: any =
						{
							id: record.defId,
							text: record.label || record.logicalName,
							type: 'record'
						};
					nodes.push(node);

					if (record.relations)
					{
						node.children = [];

						for (let relation of record.relations)
						{
							node.children.push(component.parseRelation(relation));
						}
					}
				}

				callback(nodes);
				setTimeout(selectNode, 100);
			}

		treeElement.jstree(
			{
				core:
					{
						data: fillTree,
						themes:
							{
								dots: true,
								ellipsis: true
							}
					},
				sort:
					function (node1, node2)
					{
						let a1 = this.get_node(node1).text.toLowerCase();
						let b1 = this.get_node(node2).text.toLowerCase();

						return a1 > b1 ? 1 : (a1 < b1 ? -1 : 0);
					},
				plugins: ['sort', 'types'],
				types:
					{
						record:
							{
								icon: 'assets/images/tree/record.png'
							},
						relation:
							{
								icon: 'assets/images/tree/relation.png'
							},
						default:
							{
								icon: 'assets/images/tree/record.png'
							}
					}
			});

		treeElement.on("select_node.jstree",
			function (e, data)
			{
				configManager.selectNode(data && data.node ? data.node.id : null);
			})
			.on("deselect_all.jstree",
			function (e, data)
			{
				configManager.selectNode(null);
			});

		this.isTreeInitialised = true;
	}

	private parseRelation(relation: Relation)
	{
		if (!relation.entityDefinition)
		{
			return;
		}

		let node: any =
			{
				id: relation.defId,
				text: relation.entityDefinition.label || relation.schemaName,
				type: 'relation'
			};

		if (relation.entityDefinition.relations)
		{
			node.children = [];

			for (let childRelation of relation.entityDefinition.relations)
			{
				node.children.push(this.parseRelation(childRelation));
			}
		}

		return node;
	}
}
