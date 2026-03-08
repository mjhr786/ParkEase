using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services
{
    public class ResendEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _fromEmail;

        public ResendEmailService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Resend:ApiKey"] 
                      ?? throw new InvalidOperationException("Resend:ApiKey is not configured.");
            _fromEmail = configuration["Resend:FromEmail"] ?? "onboarding@resend.dev"; // Default for testing

            // HttpClient Setup
            _httpClient.BaseAddress = new Uri("https://api.resend.com");
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine(">> Resend API Key missing. Email not sent.");
                return;
            }

            to = "mshaikh8992@gmail.com"; // Override for testing - Remove in production

            try 
            {
                var emailRequest = new
                {
                    from = "ParkEase <" + _fromEmail + ">",
                    to = new[] { to },
                    subject = subject,
                    html = isHtml ? body : null,
                    text = !isHtml ? body : null
                };

                var response = await _httpClient.PostAsJsonAsync("/emails", emailRequest);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error sending email to {to}: {response.StatusCode} - {error}");
                   // throw new Exception($"Resend API Error: {error}");
                   // Start with logging instead of throwing to avoid breaking main flow
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Exception sending email to {to}: {ex.Message}");
            }
        }
    }
}
