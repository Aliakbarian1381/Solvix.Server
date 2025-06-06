using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solvix.Server.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
        }

        public async Task SendNotificationAsync(AppUser user, string title, string body, Dictionary<string, string> data)
        {
            if (user == null || string.IsNullOrEmpty(user.FcmToken))
            {
                _logger.LogWarning("User {UserId} has no FCM token. Cannot send notification.", user?.Id);
                return;
            }

            var message = new FirebaseAdmin.Messaging.Message()
            {
                Token = user.FcmToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body,
                },
                Data = data,
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default" // برای پخش صدای پیش‌فرض نوتیفیکیشن در iOS
                    }
                }
            };

            try
            {
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Successfully sent FCM message to user {UserId}: {Response}", user.Id, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM message to user {UserId}", user.Id);
                // اینجا می‌تونی منطقی برای حذف توکن‌های نامعتبر هم اضافه کنی
            }
        }
    }
}