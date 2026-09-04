-- ==============================================================================
-- CebizPay Pre-Migration Production Deduplication Audit
-- Purpose: Verify that no duplicate phone numbers exist prior to executing
--          migration '20260904160000_AddUniqueIndexOnAspNetUsersPhoneNumber'.
-- Instruction:
--   If this query returns ANY rows, DO NOT apply the unique index migration.
--   Flag the duplicate records to the Compliance and Support operations team
--   for identity review and manual account ownership reconciliation.
-- ==============================================================================

SELECT 
    "PhoneNumber", 
    COUNT(*) AS DuplicateCount,
    ARRAY_AGG("Id" ORDER BY "CreatedAtUtc" ASC) AS UserIds,
    ARRAY_AGG("Email" ORDER BY "CreatedAtUtc" ASC) AS AssociatedEmails,
    ARRAY_AGG("CreatedAtUtc" ORDER BY "CreatedAtUtc" ASC) AS CreationDates
FROM "AspNetUsers"
WHERE "PhoneNumber" IS NOT NULL AND "PhoneNumber" != ''
GROUP BY "PhoneNumber"
HAVING COUNT(*) > 1;
