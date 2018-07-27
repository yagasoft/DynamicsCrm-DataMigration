export class ValueMappingViewModel
{
	isIgnoreValues: boolean;
	sourceField: { logicalName: string; displayName: string; };
	sourceValue?: string | undefined;
	destinationField: { logicalName: string; displayName: string; };
	destinationFieldType: { id: number; text: string };
	destinationValue?: string | undefined;
}
