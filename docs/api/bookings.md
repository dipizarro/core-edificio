# Facility Bookings API

Manage facility bookings, including approval, cancellation, and fine management.

## Mark No-Show

`POST /api/communities/{communityId}/facilities/{facilityId}/bookings/{bookingId}/no-show`

Mark an approved booking as a No-Show.

### Authorization
- Role: `Committee`, `Admin`
- Policy: `CommunityScope`

### Rules
- Only `Approved` bookings can be marked as `NoShow`.
- The booking must have already started (`StartAtUtc <= now`).
- Generates a `Fine` charge if `NoShowFineAmountClp` is configured for the facility.

---

## Cancel Booking

`POST /api/bookings/{bookingId}/cancel`

### Late Cancellation Fines
If the facility has `CancelPenaltyHours > 0` and `LateCancelFineAmountClp > 0`:
- If an **Approved** booking is cancelled within the penalty window (hours to start < `CancelPenaltyHours`), a `LateCancel` fine charge is generated.
- **Pending** bookings can be cancelled without penalty.

---

## Accounting & Idempotency
- **Fines**: Generated as `Charge` entities with `ChargeKind = "Fine"`.
- **Idempotency**: All fine generation is idempotent. Re-sending a no-show or cancel request for an already processed booking will not generate duplicate charges.
- **Manual Adjustments**: Charges (Rent, Deposit, Fine) are NOT automatically deleted if a booking is changed after charges are generated. They must be managed via the Billing API if adjustments are needed.
