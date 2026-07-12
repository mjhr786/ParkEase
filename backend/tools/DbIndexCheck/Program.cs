using Npgsql;
var cs = Environment.GetEnvironmentVariable("CONN")!;
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand(@"
SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname='public' AND (
  indexname LIKE 'IX_Bookings_Space%' OR indexname LIKE 'IX_Bookings_Pending%'
  OR indexname LIKE 'IX_ParkingSpaces_Public%' OR indexname LIKE 'IX_Notifications%'
  OR indexname LIKE '%UserId1%')
ORDER BY indexname;
SELECT column_name FROM information_schema.columns WHERE table_name='Notifications' AND column_name='UserId1';
", conn);
await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync()) Console.WriteLine(r.GetString(0)+" | "+r.GetString(1));
await r.NextResultAsync();
Console.WriteLine(await r.ReadAsync() ? "UserId1_STILL_EXISTS" : "UserId1_GONE");
