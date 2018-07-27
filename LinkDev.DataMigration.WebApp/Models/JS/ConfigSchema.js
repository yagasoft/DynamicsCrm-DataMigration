var t =
    {
        "$schema": "http://json-schema.org/schema#",
        "id": "http://yourdomain.com/schemas/myschema.json",
        "title": "Migration Tool Configuration Schema",
        "definitions":
        {
            "record":
            {
                "type": "object",
                "properties":
                {
                    "label": { "type": "string" },
                    "logicalName": { "type": "string" },
                    "fetchXml": { "type": "string" },
                    "relations":
                    {
                        "type": "array",
                        "items": { "$ref": "#/definitions/relation" }
                    },
                    "isDeleteObsoleteRecords": { "type": "boolean" },
                    "isUseAlternateKeysForRecord": { "type": "boolean" },
                    "isUseAlternateKeysForLookups": { "type": "boolean" },
                    "isClearInvalidLookups": { "type": "boolean" }
                },
                "required": ["logicalName"],
                "additionalProperties": false
            },
            "relation":
            {
                "type": "object",
                "properties":
                {
                    "schemaName": { "type": "string" },
                    "relationType": { "type": "number" },
                    "isDeleteObsoleteRelations": { "type": "boolean" },
                    "entityDefinition": { "$ref": "#/definitions/record" }
                },
                "required": ["schemaName", "relationType", "entityDefinition"],
                "additionalProperties": false
            }
        },
        "properties":
        {
            "exportOptions":
            {
                "type": "object",
                "properties":
                {
                    "maxThreadCount": { "type": "number" },
                    "isExcludeOwner": { "type": "boolean" }
                },
                "additionalProperties": false
            },
            "importOptions":
            {
                "type": "object",
                "properties":
                {
                    "maxThreadCount": { "type": "number" },
                    "bulkSize": { "type": "number" },
                    "isDeleteObsoleteRecords": { "type": "boolean" },
                    "isDeleteObsoleteRelations": { "type": "boolean" }
                },
                "additionalProperties": false
            },
            "records":
            {
                "type": "array",
                "items": { "$ref": "#/definitions/record" }
            }
        },
        "additionalProperties": false
    };