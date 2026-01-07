# Manual E2E Tests

## Bulk Operations

### Create 10 Units via Bulk
1. Prepare a JSON payload with 10 units.
2. Send POST request to `/api/communities/{communityId}/units/bulk`.
3. Verify response status is 200 OK.
4. Verify response summary shows `total: 10`, `created: 10`, `failed: 0`.
5. Verify all 10 items in `results` have `success: true`.

### Create 6 Expenses via Bulk
1. Prepare a JSON payload with 6 expenses.
2. Send POST request to `/api/communities/{communityId}/billing/expenses/bulk`.
3. Verify response status is 200 OK.
4. Verify response summary shows `total: 6`, `created: 6`, `failed: 0`.
5. Verify all 6 items in `results` have `success: true`.
