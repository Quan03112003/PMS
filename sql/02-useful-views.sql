USE HotelChainPms;
GO
CREATE OR ALTER VIEW dbo.v_room_status AS
SELECT r.id AS room_id, r.property_id, r.number AS room_number, r.floor,
       rt.name AS room_type_name, rt.base_rate, r.housekeeping_status, r.is_out_of_order,
       CASE WHEN r.is_out_of_order = 1 THEN 'MAINTENANCE'
            WHEN EXISTS (SELECT 1 FROM dbo.stays s JOIN dbo.reservations res ON res.id = s.reservation_id
                         WHERE s.room_id = r.id AND s.status = 'IN_HOUSE') THEN 'OCCUPIED'
            WHEN r.housekeeping_status = 'DIRTY' THEN 'DIRTY'
            ELSE 'VACANT' END AS physical_status
FROM dbo.rooms r JOIN dbo.room_types rt ON rt.id = r.room_type_id;
GO
CREATE OR ALTER VIEW dbo.v_folio_balance AS
SELECT r.id AS reservation_id, r.code AS reservation_code, r.property_id,
       COALESCE(c.total_charges, 0) AS total_charges,
       COALESCE(p.total_paid, 0) AS total_paid,
       COALESCE(c.total_charges, 0) - COALESCE(p.total_paid, 0) AS balance
FROM dbo.reservations r
OUTER APPLY (SELECT SUM(ch.amount) AS total_charges FROM dbo.folio_charges ch WHERE ch.reservation_id = r.id) c
OUTER APPLY (SELECT SUM(pay.amount) AS total_paid FROM dbo.payments pay WHERE pay.reservation_id = r.id) p;
GO
SELECT TOP (20) * FROM dbo.v_room_status ORDER BY property_id, room_number;
SELECT TOP (20) * FROM dbo.v_folio_balance ORDER BY reservation_id DESC;
