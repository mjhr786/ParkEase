using System;
using Npgsql;

namespace DbQuery {
    class Program {
        static void Main(string[] args) {
            var connStr = "Host=aws-1-ap-northeast-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.wlyyckibntfscbttjndr;Password=ParkEaseAugust2026;SSL Mode=Require;Trust Server Certificate=true;Pooling=false;";
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Status\", \"StartDateTime\", \"EndDateTime\" FROM \"Bookings\" ORDER BY \"CreatedAt\" DESC LIMIT 10", conn);
            using var reader = cmd.ExecuteReader();
            Console.WriteLine("Recent Bookings:");
            while(reader.Read()) {
                Console.WriteLine($"ID: {reader.GetGuid(0)}, Status: {reader.GetInt32(1)}, Start: {reader.GetDateTime(2):o}, End: {reader.GetDateTime(3):o}, UtcNow: {DateTime.UtcNow:o}, Active: {reader.GetDateTime(3) > DateTime.UtcNow}");
            }
        }
    }
}
