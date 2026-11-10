-- Fax, email and website for the company header on reports.
-- Safe to run more than once. Apply this before the API that selects these columns.

ALTER TABLE properties ADD COLUMN IF NOT EXISTS fax character varying(30);
ALTER TABLE properties ADD COLUMN IF NOT EXISTS email character varying(120);
ALTER TABLE properties ADD COLUMN IF NOT EXISTS website character varying(200);
