# Bulk Operations

This document describes the API endpoints for performing bulk operations.

## Common Response Format

All bulk operations return a response with the following structure:

```json
{
  "communityId": "uuid",
  "total": "number", // Total items processed
  "created": "number", // Number of successfully created items
  "failed": "number", // Number of failed items
  "results": [
    {
      "index": "number", // Index from the original request array
      "success": "boolean",
      "error": "string | null", // Error message if failed
      "data": "object | null" // Created entity data if success
    }
  ]
}
```

## Endpoints

### Create Units in Bulk

Create multiple units in a community at once.

**Endpoint:** `POST /api/communities/{communityId}/units/bulk`

**Request Body:**

```json
{
  "units": [
    {
      // CreateUnitCommand fields
      "name": "string",
      "type": "string",
      // ... other fields
    }
  ]
}
```

### Create Expenses in Bulk

Create multiple expenses in a community at once.

**Endpoint:** `POST /api/communities/{communityId}/billing/expenses/bulk`

**Request Body:**

```json
{
  "expenses": [
    {
      // CreateExpenseCommand fields
      "description": "string",
      "amount": "number",
      // ... other fields
    }
  ]
}
```
