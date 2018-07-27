import { SignalrService } from './services/signalr.service';
import { StateService } from './services/state.service';
import { MessageService } from 'primeng/components/common/messageservice';
import { CrmDataService } from './services/crm-data.service';
import { HttpClientModule } from '@angular/common/http';

import { Routes, RouterModule } from '@angular/router';
import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SimpleNotificationsModule } from 'angular2-notifications';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

import { AppComponent } from './app.component';
import { ExportDataComponent } from './operations/export-data/export-data.component';
import { ExportOptionsComponent } from './options/export-options/export-options.component';
import { CrmConnectComponent } from './crm-connect/crm-connect.component';
import { OperationsComponent } from './operations/operations.component';
import { NavigationBarComponent } from './navigation-bar/navigation-bar.component';
import { EntitiesTreeComponent } from './entities-tree/entities-tree.component';
import { EntitiesTreeTreeComponent } from './entities-tree/entities-tree-tree/entities-tree-tree.component';
import { EntitySelectorComponent } from './entities-tree/entity-selector/entity-selector.component';
import { RecordFormComponent } from "./record-form/record-form.component";
import { ImportDataComponent } from './operations/import-data/import-data.component';

import { NotificationService } from './services/notification.service';
import { CrmConnectionService } from './services/crm-connection.service';
import { ConfigManagementService } from './services/config-management.service';
import { CrmMetadataService } from './services/crm-metadata.service';

import { AutoCompleteModule, DialogModule, ConfirmDialogModule, ConfirmationService, DropdownModule } from 'primeng/primeng';
import { JasperoAlertsModule } from '@jaspero/ng2-alerts';
import { SelectModule } from 'ng2-select';

import { OptionsComponent } from './options/options.component';
import { ImportOptionsComponent } from './options/import-options/import-options.component';
import { SimpleMappingComponent } from './mapping/simple-mapping/simple-mapping.component';

const appRoutes: Routes =
	[
		{
			path: 'operations',
			component: OperationsComponent,
			children:
				[
					{
						path: 'export/:defId',
						component: ExportDataComponent,
					},
					{
						path: 'export',
						component: ExportDataComponent
					}
					,
					{
						path: 'import',
						component: ImportDataComponent
					}
				]
		},
		{ path: '**', component: CrmConnectComponent },
	];

@NgModule(
	{
		declarations:
			[
				AppComponent,
				ExportDataComponent,
				ImportDataComponent,
				CrmConnectComponent,
				OperationsComponent,
				NavigationBarComponent,
				EntitiesTreeComponent,
				EntitiesTreeTreeComponent,
				EntitySelectorComponent,
				RecordFormComponent,
				ExportOptionsComponent,
				OptionsComponent,
				ImportOptionsComponent,
				SimpleMappingComponent
			],
		imports:
			[
				BrowserModule,
				HttpClientModule,
				FormsModule,
				ReactiveFormsModule,
				RouterModule.forRoot(appRoutes),
				BrowserAnimationsModule,
				SimpleNotificationsModule.forRoot(),
				JasperoAlertsModule,
				AutoCompleteModule,
				ConfirmDialogModule,
				DialogModule,
				DropdownModule,
				SelectModule
			],
		providers:
			[
				CrmConnectionService,
				CrmMetadataService,
				ConfigManagementService,
				CrmDataService,
				NotificationService,
				ConfirmationService,
				StateService,
				SignalrService
			],
		bootstrap: [AppComponent]
	})
export class AppModule { }
