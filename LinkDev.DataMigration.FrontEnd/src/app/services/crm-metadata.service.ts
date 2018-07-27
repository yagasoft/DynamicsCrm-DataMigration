import { Observable } from 'rxjs/Observable';
import { HttpClientModule, HttpClient } from '@angular/common/http';
import { EventEmitter, Injectable, Output } from '@angular/core';
import { RequestOptions } from '@angular/http/src/base_request_options';

@Injectable()
export class CrmMetadataService {
	// TODO
	private baseUrl: string = 'http://localhost:58001/api/entities'

	constructor(private http: HttpClient) { }

	getEntityNames()
	{
		return this.http.get(this.baseUrl);
	}

	getEntityMetadata(logicalName: string)
	{
		return this.http.get(`${this.baseUrl}/${logicalName}`);
	}
}
