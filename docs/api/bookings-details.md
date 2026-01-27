# Booking Details & UI Flows

New endpoints to support detailed booking views and administrative flows.

## Get Booking Details

`GET /api/bookings/{bookingId}`

Returns full details including charges.

### Authorization
- **Resident**: Can only view their own unit's bookings.
- **Committee/Admin**: Can view any booking in their community.

### Response
Returns `BookingDetailDto`:
- Basic info (Id, Facility, Unit, Dates, Status)
- `Charges`: List of generated charges (Rent, Deposit, Fines).
- `CompletedAtUtc`: Timestamp if completed.

## List Bookings (Enhanced)

`GET /api/communities/{communityId}/bookings`

Administrative list of bookings with filtering.

### Authorization
- Role: `Committee`, `Admin`

### Query Parameters
- `from`, `to`: Date range (UTC).
- `facilityId`: Filter by facility.
- `status`: Filter by status (PendingApproval, Approved, etc).

### Response
Returns `List<BookingListDto>` which includes `FacilityName` and `UnitNumber`.

## My Bookings (Enhanced)

`GET /api/bookings/my`

Same filters as above. Returns `BookingListDto`.

## Complete Booking

`POST /api/bookings/{bookingId}/complete`

Mark an **Approved** booking as **Completed**. 
- Used by staff to confirm the event took place or finished.
- Sets `Status = Completed` and `CompletedAtUtc = Now`.
- Does NOT generate extra charges.

### Authorization
- Role: `Committee`, `Admin`
