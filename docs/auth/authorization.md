\# CoreEdificio — Authorization Model



\## Goals

\- Enforce access rules in backend (never trust route params alone).

\- Keep a simple model that can scale to multiple communities and future frontends.



\## Roles

\- \*\*Admin\*\*: global super-user for support/debugging.

\- \*\*Committee\*\*: manages a single community.

\- \*\*Resident\*\*: can only view their own unit information.



\## Scope (Claims)

JWT includes scope claims:

\- `communityId` (Guid): the community the user belongs to (Committee/Resident)

\- `unitId` (Guid): the unit the user belongs to (Resident only)

\- `roles`: standard role claims



\### Source of truth

\*\*JWT claims are the source of truth\*\* for scope.  

Route params (e.g., `communityId` in URL) are never trusted without validating against claims.



\## Access Matrix



\### Admin (Global scope)

Can access everything across communities:

\- Communities, units, billing, expenses, payments, balances, arrears.



\### Committee (Community scope)

Can access everything within their `communityId`:

\- Create expenses

\- Issue billing period

\- Register payments

\- View balances (any unit in their community)

\- View arrears/morosidad (community period)



Cannot:

\- Access other communities

\- Perform global administration tasks



\### Resident (Unit scope)

Can view only their own unit information:

\- Own unit balance (for a period)

\- Own payment history



Cannot:

\- View other units

\- View community-wide arrears

\- Create expenses or register payments

\- Issue billing period



\## Endpoint Rules (high level)

\- Write operations (expenses, issue, payments): \*\*Admin or Committee\*\*

\- Community-wide reads (arrears, balances by unit): \*\*Admin or Committee\*\*

\- Self reads (my balance, my payments): \*\*Resident (or higher)\*\*



