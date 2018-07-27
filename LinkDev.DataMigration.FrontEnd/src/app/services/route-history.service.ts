import { ActivatedRoute, Router, NavigationEnd } from '@angular/router';
import { Injectable } from '@angular/core';
import 'rxjs/add/operator/filter';

@Injectable()
export class RouteHistoryService
{
	private urlHistory: string[] = [];

	constructor(private router: Router)
	{
		this.router.events
			.filter(event => event instanceof NavigationEnd)
			.subscribe((event: NavigationEnd) =>
			{
				this.urlHistory.push(event.url);
			});
	}

	getPreviousUrl(): string
	{
		const length = this.urlHistory.length;

		if (length >= 2)
		{
			return this.urlHistory[length - 2];
		}
	}
}
