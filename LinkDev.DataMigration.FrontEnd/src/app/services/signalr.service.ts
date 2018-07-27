import { EventEmitter } from '@angular/core';
import { Injectable } from '@angular/core';
import { HubConnection } from '@aspnet/signalr-client';

declare var $: any;

@Injectable()
export class SignalrService
{
	private hubConnection: HubConnection;
	logEntryAdded = new EventEmitter<{ entry: string, level: string }>();
	
	constructor()
	{
		var connection = $.hubConnection('http://localhost:58001');
		var hubProxy = connection.createHubProxy('progressHub');

		hubProxy.on('publishProgress',
			(id, message, progress) =>
			{
				console.log(id, message, progress);
			});
		
		hubProxy.on('publishLog',
			(message, date, level, details) =>
			{
				if (level !== 'None' && level !== 'Debug')
				{
					this.logEntryAdded.emit({ entry: `${date}: [${level}] ${message}${details ? ` |=> ${details}` : ''}`, level: level });
				}
			});
		
		connection.start()
			.done(() =>
			{
				console.log('Now connected, connection ID=' + connection.id);

				hubProxy.invoke('SubscribeToProgress')
					.done(() =>
					{
						console.log('Invocation of SubscribeToProgress succeeded');
					}).fail((error) =>
					{
						console.error('Invocation of SubscribeToProgress failed. Error: ' + error);
					});
				
				hubProxy.invoke('SubscribeToLog')
					.done(() =>
					{
						console.log('Invocation of SubscribeToLog succeeded');
					}).fail((error) =>
					{
						console.error('Invocation of SubscribeToLog failed. Error: ' + error);
					});

			})
			.fail(error => console.error(error));

		// this.nick = window.prompt('Your name:', 'John');

		// this.hubConnection = new HubConnection('http://localhost:58001/signalr');

		// this.hubConnection
		// 	.start()
		// 	.then(() => console.log('Connection started!'))
		// 	.catch(err => console.error(err));

		// this.hubConnection.on('broadcastMessage', (nick: string, receivedMessage: string) =>
		// {
		// 	const text = `${nick}: ${receivedMessage}`;
		// 	this.messages.push(text);
		// 	console.log(nick, receivedMessage);
		// });
	}
}
