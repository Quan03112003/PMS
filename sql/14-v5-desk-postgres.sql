-- Nationality report uses guests.nationality already present.
-- Price floor, confidential rate, and approved debt exposure.
-- Additive. Safe to re-run. Apply before the API that selects these columns.

ALTER TABLE room_types ADD COLUMN IF NOT EXISTS min_nightly_rate numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE room_types ADD COLUMN IF NOT EXISTS max_discount_percent numeric(5,2) NOT NULL DEFAULT 15;

ALTER TABLE reservations ADD COLUMN IF NOT EXISTS rate_confidential boolean NOT NULL DEFAULT false;
ALTER TABLE reservations ADD COLUMN IF NOT EXISTS approved_exposure numeric(18,2) NOT NULL DEFAULT 0;
