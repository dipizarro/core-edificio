# Bulk Unit Creation with Components

Creates multiple units, each with one or more components (e.g., Department, Parking), in a single request.

## Endpoint
`POST /api/communities/{communityId}/units/bulk-with-components`

## Authorization
- Roles: `Committee`, `Admin`
- Scope: `CommunityScope` (User must belong to the specified community)

## Request Body
```json
{
  "units": [
    {
      "unitNumber": "101",
      "components": [
        { "type": "Department", "code": "0904", "coefficientPct": 0.998 },
        { "type": "Parking", "code": "E-100", "coefficientPct": 0.099 },
        { "type": "Storage", "code": "B-92", "coefficientPct": 0.028 }
      ]
    },
    {
      "unitNumber": "102",
      "components": [
        { "type": "Department", "code": "0905", "coefficientPct": 1.12 }
      ]
    }
  ]
}
```

## Response
Returns a `BulkResponse` object indicating the number of units created and failed, along with detailed results for each item.

## curl Example
```bash
curl -X POST "https://api.coreedificio.local/api/communities/3fa85f64-5717-4562-b3fc-2c963f66afa6/units/bulk-with-components" \
     -H "Authorization: Bearer YOUR_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "units": [
         {
           "unitNumber": "101",
           "components": [
             { "type": "Department", "code": "0904", "coefficientPct": 1.0 }
           ]
         }
       ]
     }'
```
