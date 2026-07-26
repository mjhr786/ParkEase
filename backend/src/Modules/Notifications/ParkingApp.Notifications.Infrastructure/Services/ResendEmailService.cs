using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Notifications.Infrastructure.Services
{
    internal sealed class ResendEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _fromEmail;

        public ResendEmailService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Resend:ApiKey"] ?? string.Empty;
            _fromEmail = configuration["Resend:FromEmail"] ?? "onboarding@resend.dev"; // Default for testing

            // HttpClient Setup
            _httpClient.BaseAddress = new Uri("https://api.resend.com");
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public Task SendEmailAsync(string to, string subject, string body, bool isHtml = true) =>
            SendEmailAsync(to, subject, body, attachments: null, isHtml);

        public async Task SendEmailAsync(
            string to,
            string subject,
            string body,
            IReadOnlyList<EmailAttachment>? attachments,
            bool isHtml = true)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine(">> Resend API Key missing. Email not sent.");
                return;
            }

            to = "mshaikh8992@gmail.com"; // Override for testing - Remove in production

            try
            {
                object emailRequest;
                if (attachments is { Count: > 0 })
                {
                    emailRequest = new
                    {
                        from = "ParkEase <" + _fromEmail + ">",
                        to = new[] { to },
                        subject = subject,
                        html = isHtml ? body : null,
                        text = !isHtml ? body : null,
                        attachments = attachments.Select(a => new
                        {
                            filename = a.FileName,
                            content = Convert.ToBase64String(a.Content),
                            content_type = a.ContentType
                        }).ToArray()
                    };
                }
                else
                {
                    emailRequest = new
                    {
                        from = "ParkEase <" + _fromEmail + ">",
                        to = new[] { to },
                        subject = subject,
                        html = isHtml ? body : null,
                        text = !isHtml ? body : null
                    };
                }

                var response = await _httpClient.PostAsJsonAsync("/emails", emailRequest);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error sending email to {to}: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception sending email to {to}: {ex.Message}");
            }
        }
    }
}
