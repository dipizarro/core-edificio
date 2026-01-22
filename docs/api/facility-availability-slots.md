# Facility Availability Slots API

Returns a list of discrete time slots for a specific facility, indicating if each slot is Free, Booked, Pending, or Blocked.

## Get Availability Slots

`GET /api/communities/{communityId}/facilities/{facilityId}/availability/slots?from={start}&to={end}`

### Parameters

- `communityId` (path): The unique ID of the community.
- `facilityId` (path): The unique ID of the facility.
- `from` (query): Start of the search range (ISO 8601 UTC).
- `to` (query): End of the search range (ISO 8601 UTC).

### Constraints
- `from` must be before `to`.
- The range (`to - from`) cannot exceed **14 days**.
- Slots are generated based on the facility's `SlotDurationMinutes`.

### Recommendation
For the best experience, the client should send `from` and `to` aligned to the facility's `SlotDurationMinutes` (e.g., if slots are 60 minutes, send values on the hour).

### Status Priorities
If multiple events overlap a single slot:
1. `Blocked` (Highest priority)
2. `Booked` (Approved booking)
3. `Pending` (Pending approval booking)
4. `Free` (Lowest priority)

### Response Example

```json
{
  "communityId": "d5f8e3a2-...",
  "facilityId": "e1a2b3c4-...",
  "fromUtc": "2026-02-01T10:00:00Z",
  "toUtc": "2026-02-01T12:00:00Z",
  "slotMinutes": 60,
  "slots": [
    {
      "startAtUtc": "2026-02-01T10:00:00Z",
      "endAtUtc": "2026-02-01T11:00:00Z",
      "status": "Booked",
      "reasonOrStatus": "Approved",
      "bookingId": "...",
      "blockId": null
    },
    {
      "startAtUtc": "2026-02-01T11:00:00Z",
      "endAtUtc": "2026-02-01T12:00:00Z",
      "status": "Free",
      "reasonOrStatus": null,
      "bookingId": null,
      "blockId": null
    }
  ]
}
```
