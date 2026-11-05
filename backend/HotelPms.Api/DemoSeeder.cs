using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class DemoSeeder
{
    public static async Task Seed(PmsDbContext db, IPasswordHasher<AppUser> hasher)
    {
        if (await db.Properties.AnyAsync()) return;
        var p1 = new Property { Code = "HN-CENTRAL", Name = "Lotus Central Hà Nội", City = "Hà Nội", Address = "Quận Hoàn Kiếm, Hà Nội", Phone = "024-8888-1001" };
        var p2 = new Property { Code = "DN-RIVERSIDE", Name = "Lotus Riverside Đà Nẵng", City = "Đà Nẵng", Address = "Quận Hải Châu, Đà Nẵng", Phone = "0236-8888-1002" };
        db.Properties.AddRange(p1, p2); await db.SaveChangesAsync();
        var types = new[] {
            new RoomType { PropertyId = p1.Id, Code = "STD", Name = "Standard", Capacity = 2, BaseRate = 650000 },
            new RoomType { PropertyId = p1.Id, Code = "DLX", Name = "Deluxe", Capacity = 2, BaseRate = 950000 },
            new RoomType { PropertyId = p1.Id, Code = "SUITE", Name = "Executive Suite", Capacity = 4, BaseRate = 1650000 },
            new RoomType { PropertyId = p2.Id, Code = "STD", Name = "Standard", Capacity = 2, BaseRate = 590000 },
            new RoomType { PropertyId = p2.Id, Code = "DLX", Name = "Ocean Deluxe", Capacity = 3, BaseRate = 1100000 }
        };
        db.RoomTypes.AddRange(types); await db.SaveChangesAsync();
        var rooms = new List<Room>();
        for (var floor = 1; floor <= 3; floor++) for (var i = 1; i <= 6; i++) rooms.Add(new Room {
            PropertyId = p1.Id, RoomTypeId = i == 6 ? types[2].Id : i >= 4 ? types[1].Id : types[0].Id,
            Number = $"{floor}{i:00}", Floor = floor,
            HousekeepingStatus = floor == 3 && i == 4 ? "DIRTY" : "INSPECTED"
        });
        for (var floor = 2; floor <= 3; floor++) for (var i = 1; i <= 5; i++) rooms.Add(new Room {
            PropertyId = p2.Id, RoomTypeId = i >= 4 ? types[4].Id : types[3].Id,
            Number = $"{floor}{i:00}", Floor = floor
        });
        db.Rooms.AddRange(rooms); await db.SaveChangesAsync();
        var svcs = new[] {
            new ServiceItem { PropertyId = p1.Id, Code = "MIN-WATER", Name = "Nước suối 500ml", Unit = "Chai", Price = 15000, TrackInventory = true, StockQuantity = 120 },
            new ServiceItem { PropertyId = p1.Id, Code = "MIN-SODA", Name = "Nước ngọt", Unit = "Lon", Price = 25000, TrackInventory = true, StockQuantity = 80 },
            new ServiceItem { PropertyId = p1.Id, Code = "LAUNDRY", Name = "Giặt là", Unit = "Lượt", Price = 80000 },
            new ServiceItem { PropertyId = p1.Id, Code = "EXTRA-BED", Name = "Giường phụ", Unit = "Đêm", Price = 250000 },
            new ServiceItem { PropertyId = p1.Id, Code = "AIRPORT", Name = "Đưa đón sân bay", Unit = "Lượt", Price = 350000 },
            new ServiceItem { PropertyId = p2.Id, Code = "MIN-WATER", Name = "Nước suối", Unit = "Chai", Price = 15000, TrackInventory = true, StockQuantity = 50 },
            new ServiceItem { PropertyId = p2.Id, Code = "AIRPORT", Name = "Xe đưa đón sân bay", Unit = "Lượt", Price = 300000 }
        };
        db.Services.AddRange(svcs);
        db.Agencies.AddRange(
            new Agency { PropertyId = p1.Id, Code = "BOOKING", Name = "Booking.com", CommissionPercent = 15 },
            new Agency { PropertyId = p1.Id, Code = "AGODA", Name = "Agoda", CommissionPercent = 16 },
            new Agency { PropertyId = p1.Id, Code = "LOTUS-TRAVEL", Name = "Lotus Travel", CommissionPercent = 10 }
        );
        var admin = new AppUser { FullName = "Quản trị chuỗi", Email = "admin@pms.local", Role = "CHAIN_ADMIN" };
        var manager = new AppUser { FullName = "Quản lý Hà Nội", Email = "manager@pms.local", Role = "PROPERTY_MANAGER", PropertyId = p1.Id };
        var reception = new AppUser { FullName = "Lễ tân Demo", Email = "reception@pms.local", Role = "FRONT_DESK_AGENT", PropertyId = p1.Id };
        foreach (var u in new[] { admin, manager, reception }) u.PasswordHash = hasher.HashPassword(u, "Demo@12345");
        db.Users.AddRange(admin, manager, reception); await db.SaveChangesAsync();
        db.Shifts.Add(new CashShift { PropertyId = p1.Id, UserId = reception.Id, OpeningAmount = 2000000 });
        var guestNames = new[] { "Nguyễn Minh Anh", "Trần Hoàng Nam", "Phạm Thu Hà", "Lê Quốc Bảo", "Đỗ Thanh Hương", "Vũ Hải Đăng", "Ngô Kiều Oanh", "Hoàng Gia Huy" };
        var guests = guestNames.Select((name, i) => new Guest { FullName = name, Phone = "09" + (i + 1).ToString("00000000") }).ToList();
        db.Guests.AddRange(guests); await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var demoRooms = rooms.Where(x => x.PropertyId == p1.Id && x.HousekeepingStatus == "INSPECTED").Take(8).ToArray();
        var reservations = new List<Reservation>();
        for (int i = 0; i < 8; i++) {
            var r = new Reservation { Code = $"BK-DEMO-{i + 1:000}", PropertyId = p1.Id, RoomId = demoRooms[i].Id, GuestId = guests[i].Id,
                CheckInDate = today.AddDays(i < 3 ? -1 : i < 6 ? 0 : 1), CheckOutDate = today.AddDays(i < 3 ? 2 : i < 6 ? 2 : 4),
                NightlyRate = i == 5 ? 1650000 : i >= 3 ? 950000 : 650000,
                BookingSource = i % 3 == 0 ? "OTA" : "DIRECT", Status = i < 3 ? "CHECKED_IN" : "CONFIRMED", Adults = i % 4 == 0 ? 2 : 1 };
            reservations.Add(r);
        }
        db.Reservations.AddRange(reservations); await db.SaveChangesAsync();
        for (int i = 0; i < 3; i++) {
            var r = reservations[i]; db.Stays.Add(new Stay { ReservationId = r.Id, RoomId = r.RoomId, CheckedInAtUtc = DateTime.UtcNow.AddHours(-14) });
            var nights = r.CheckOutDate.DayNumber - r.CheckInDate.DayNumber;
            db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "ROOM", Description = $"Tiền phòng {nights} đêm", Quantity = nights,
                UnitPrice = r.NightlyRate, Amount = nights * r.NightlyRate });
            if (i == 0) db.Payments.Add(new Payment { ReservationId = r.Id, Amount = 1000000, Method = "TRANSFER", Kind = "PAYMENT" });
        }
        await db.SaveChangesAsync();
    }
}
