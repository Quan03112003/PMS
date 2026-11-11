ALTER TABLE guests ADD COLUMN IF NOT EXISTS gift_note character varying(200);
ALTER TABLE reservations ADD COLUMN IF NOT EXISTS loyalty_tier character varying(10);
ALTER TABLE reservations ADD COLUMN IF NOT EXISTS loyalty_percent numeric(5,2) NOT NULL DEFAULT 0;
