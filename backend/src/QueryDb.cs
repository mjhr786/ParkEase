using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Domain.Entities;

namespace DbQuery {
    class Program {
        static void Main(string[] args) {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql("Host=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.wlyyckibntfscbttjndr;Password=ParkEaseAugust2026;SSL Mode=Require;Trust Server Certificate=true");
            using var context = new ApplicationDbContext(optionsBuilder.Options);
            var bookings = context.Bookings.Where(b => b.Status != ParkingApp.Domain.Enums.BookingStatus.Cancelled).ToList();
            Console.WriteLine("Total Active Bookings: " + bookings.Count);
            foreach(var b in bookings) {
                Console.WriteLine($"ID: {b.Id}, Status: {b.Status}, Start: {b.StartDateTime:o}, End: {b.EndDateTime:o}, UtcNow: {DateTime.UtcNow:o}, IsActive: {b.EndDateTime > DateTime.UtcNow}");
            }
        }
    }
}
