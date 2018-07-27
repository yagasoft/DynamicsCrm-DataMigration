import { EventEmitter } from '@angular/core';
import { Injectable } from '@angular/core';

@Injectable()
export class StateService
{
	changedMode = new EventEmitter<string>();

	private _mode: string;

	get mode(): string
	{
		return this._mode;
	}
	
	set mode(theMode: string)
	{
		this._mode = theMode;
		this.changedMode.emit(this.mode);
	}
	
	constructor()
	{}
}
