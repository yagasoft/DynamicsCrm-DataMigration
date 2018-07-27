export class Helpers
{
	static buildResponseErrorMessage(response): string
	{
		let message = '';

		if (!response)
		{
			return message;
		}

		if (response.message)
		{
			message += response.message;
		}

		if (response.error)
		{
			const error = response.error;

			if (error.message)
			{
				message += `${message ? ' | ' : ''}${error.message}`;
			}

			if (error.ExceptionMessage)
			{
				message += `${message ? ' | ' : ''}${error.ExceptionMessage}`;
			}
		}

		return message;
	}
}
