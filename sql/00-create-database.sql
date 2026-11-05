/* OPTIONAL - Chạy tại kết nối master trong DBeaver nếu SQL Server đã có sẵn.
   Không cần chạy khi API Development tự tạo DB bằng EF Core EnsureCreated.
*/
IF DB_ID(N'HotelChainPms') IS NULL
BEGIN
    CREATE DATABASE HotelChainPms;
END;
GO
SELECT name, state_desc FROM sys.databases WHERE name = N'HotelChainPms';
GO
