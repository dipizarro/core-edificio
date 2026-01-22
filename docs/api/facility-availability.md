# Facility Availability API

Returns a list of occupied intervals (bookings and facility blocks) for a specific facility within a date range.

## Get Availability

`GET /api/communities/{communityId}/facilities/{facilityId}/availability?from={start}&to={end}`

### Parameters

- `communityId` (path): The unique ID of the community.
- `facilityId` (path): The unique ID of the facility.
- `from` (query): Start of the search range (ISO 8601 UTC).
- `to` (query): End of the search range (ISO 8601 UTC).

### Constraints
- `from` must be before `to`.
- The range (`to - from`) cannot exceed **31 days**.

### Response Example

```json
{
  "communityId": "d5f8e3a2-...",
  "facilityId": "e1a2b3c4-...",
  "fromUtc": "2026-02-01T00:00:00Z",
  "toUtc": "2026-02-08T00:00:00Z",
  "intervals": [
    {
      "kind": "Booking",
      "startAtUtc": "2026-02-01T10:00:00Z",
      "endAtUtc": "2026-02-01T11:00:00Z",
      "statusOrReason": "Approved",
      "bookingId": "...",
      "blockId": null,
      "unitNumber": "101",
      "isTentative": false
    },
    {
      "kind": "Block",
      "startAtUtc": "2026-02-03T08:00:00Z",
      "endAtUtc": "2026-02-03T20:00:00Z",
      "statusOrReason": "Maintenance",
      "bookingId": null,
      "blockId": "...",
      "unitNumber": null,
      "isTentative": false
    }
  ]
}
```

> [!NOTE]
> The `unitNumber` field is only returned for users with `Committee` or `Admin` roles. For `Resident` users, it will be `null`.
