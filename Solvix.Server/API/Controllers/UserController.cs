using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Solvix.Server.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : BaseController
    {
        private readonly IUserService _userService;
        private readonly IUserConnectionService _connectionService;

        public UserController(
            IUserService userService,
            IUserConnectionService connectionService,
            ILogger<UserController> logger) : base(logger)
        {
            _userService = userService;
            _connectionService = connectionService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new List<UserDto>());
                }

                var currentUserId = GetUserId();
                var userDtos = await _userService.SearchUsersAsync(query, currentUserId);

                return Ok(userDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to search users");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with query: {Query}", query);
                return ServerError("خطا در جستجوی کاربران");
            }
        }

        [HttpPost("sync-contacts")]
        public async Task<IActionResult> SyncContacts([FromBody] List<string> phoneNumbers)
        {
            try
            {
                var currentUserId = GetUserId();
                var users = await _userService.FindUsersByPhoneNumbersAsync(phoneNumbers, currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing contacts for user {UserId}", GetUserId());
                return ServerError("خطا در همگام‌سازی مخاطبین");
            }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUser(long userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "کاربر یافت نشد" });
                }

                bool isOnline = await _connectionService.IsUserOnlineAsync(userId);
                var userDto = MappingHelper.MapToUserDto(user, isOnline);

                return Ok(userDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get user {UserId}", userId);
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", userId);
                return ServerError("خطا در دریافت اطلاعات کاربر");
            }
        }

        [HttpGet("saved-contacts")]
        public async Task<IActionResult> GetSavedContacts()
        {
            try
            {
                var currentUserId = GetUserId();
                var users = await _userService.GetSavedContactsAsync(currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین ذخیره شده");
            }
        }

        [HttpGet("saved-contacts-with-chat")]
        public async Task<IActionResult> GetSavedContactsWithChat()
        {
            try
            {
                var currentUserId = GetUserId();
                var users = await _userService.GetSavedContactsWithChatInfoAsync(currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts with chat info for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین با اطلاعات چت");
            }
        }

        [HttpGet("online")]
        public async Task<IActionResult> GetOnlineUsers()
        {
            try
            {
                var onlineAppUsers = await _connectionService.GetOnlineUsersAsync();

                var userDtos = onlineAppUsers
                    .Select(user => MappingHelper.MapToUserDto(user, true))
                    .ToList();

                return Ok(userDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get online users");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online users");
                return ServerError("خطا در دریافت کاربران آنلاین");
            }
        }

        [HttpPost("update-fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] FcmTokenDto tokenDto)
        {
            if (tokenDto == null || string.IsNullOrWhiteSpace(tokenDto.Token))
            {
                return BadRequest(new { message = "FCM token cannot be empty." });
            }

            try
            {
                var userId = GetUserId();
                var result = await _userService.UpdateFcmTokenAsync(userId, tokenDto.Token);

                if (result)
                {
                    return Ok(new { message = "FCM token updated successfully." });
                }
                return ServerError("Failed to update FCM token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateFcmToken endpoint for user {UserId}", GetUserId());
                return ServerError("An unexpected error occurred while updating the token.");
            }
        }

        [HttpPost("contacts/{contactId}/favorite")]
        public async Task<IActionResult> SetContactFavorite(long contactId, [FromBody] bool isFavorite)
        {
            try
            {
                var currentUserId = GetUserId();
                var result = await _userService.MarkContactAsFavoriteAsync(currentUserId, contactId, isFavorite);

                if (result)
                {
                    return Ok(new { message = isFavorite ? "مخاطب به علاقه‌مندی‌ها اضافه شد" : "مخاطب از علاقه‌مندی‌ها حذف شد" });
                }
                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting contact {ContactId} favorite status for user {UserId}", contactId, GetUserId());
                return ServerError("خطا در تنظیم وضعیت علاقه‌مندی مخاطب");
            }
        }

        [HttpPost("contacts/{contactId}/block")]
        public async Task<IActionResult> BlockContact(long contactId, [FromBody] bool isBlocked)
        {
            try
            {
                var currentUserId = GetUserId();
                var result = await _userService.BlockContactAsync(currentUserId, contactId, isBlocked);

                if (result)
                {
                    return Ok(new { message = isBlocked ? "مخاطب مسدود شد" : "مخاطب از حالت مسدود خارج شد" });
                }
                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking contact {ContactId} for user {UserId}", contactId, GetUserId());
                return ServerError("خطا در مسدود کردن مخاطب");
            }
        }

        [HttpPost("update-last-active")]
        public async Task<IActionResult> UpdateLastActive()
        {
            try
            {
                var userId = GetUserId();
                var result = await _userService.UpdateUserLastActiveAsync(userId);

                if (result)
                {
                    return Ok(new { message = "Last active time updated successfully." });
                }
                return ServerError("Failed to update last active time.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last active time for user {UserId}", GetUserId());
                return ServerError("An unexpected error occurred while updating last active time.");
            }
        }

        [HttpGet("contacts/search")]
        public async Task<IActionResult> SearchContacts([FromQuery] string query, [FromQuery] int limit = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new List<UserDto>());
                }

                var currentUserId = GetUserId();
                var contacts = await _userService.SearchContactsAsync(currentUserId, query, limit);
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts for user {UserId}", GetUserId());
                return ServerError("خطا در جستجوی مخاطبین");
            }
        }

        [HttpGet("contacts/favorites")]
        public async Task<IActionResult> GetFavoriteContacts()
        {
            try
            {
                var currentUserId = GetUserId();
                var favorites = await _userService.GetFavoriteContactsAsync(currentUserId);
                return Ok(favorites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorite contacts for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین مورد علاقه");
            }
        }

        [HttpGet("contacts/recent")]
        public async Task<IActionResult> GetRecentContacts([FromQuery] int limit = 10)
        {
            try
            {
                var currentUserId = GetUserId();
                var recent = await _userService.GetRecentContactsAsync(currentUserId, limit);
                return Ok(recent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent contacts for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین اخیر");
            }
        }

        [HttpPut("contacts/{contactId}/favorite")]
        public async Task<IActionResult> ToggleFavoriteContact(long contactId, [FromBody] FavoriteContactDto favoriteDto)
        {
            try
            {
                var currentUserId = GetUserId();
                var success = await _userService.MarkContactAsFavoriteAsync(currentUserId, contactId, favoriteDto.IsFavorite);

                if (success)
                {
                    return Ok(new { message = favoriteDto.IsFavorite ? "مخاطب به علاقه‌مندی‌ها اضافه شد" : "مخاطب از علاقه‌مندی‌ها حذف شد" });
                }

                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling favorite status for contact {ContactId}, user {UserId}", contactId, GetUserId());
                return ServerError("خطا در تغییر وضعیت علاقه‌مندی");
            }
        }

        [HttpPut("contacts/{contactId}/block")]
        public async Task<IActionResult> ToggleBlockContact(long contactId, [FromBody] BlockContactDto blockDto)
        {
            try
            {
                var currentUserId = GetUserId();
                var success = await _userService.BlockContactAsync(currentUserId, contactId, blockDto.IsBlocked);

                if (success)
                {
                    return Ok(new { message = blockDto.IsBlocked ? "مخاطب مسدود شد" : "مخاطب از مسدودیت خارج شد" });
                }

                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling block status for contact {ContactId}, user {UserId}", contactId, GetUserId());
                return ServerError("خطا در تغییر وضعیت مسدودیت");
            }
        }

        [HttpPut("contacts/{contactId}/display-name")]
        public async Task<IActionResult> UpdateContactDisplayName(long contactId, [FromBody] UpdateDisplayNameDto displayNameDto)
        {
            try
            {
                var currentUserId = GetUserId();
                var success = await _userService.UpdateContactDisplayNameAsync(currentUserId, contactId, displayNameDto.DisplayName);

                if (success)
                {
                    return Ok(new { message = "نام نمایشی مخاطب به‌روزرسانی شد" });
                }

                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating display name for contact {ContactId}, user {UserId}", contactId, GetUserId());
                return ServerError("خطا در به‌روزرسانی نام نمایشی");
            }
        }

        [HttpDelete("contacts/{contactId}")]
        public async Task<IActionResult> RemoveContact(long contactId)
        {
            try
            {
                var currentUserId = GetUserId();
                var success = await _userService.RemoveContactAsync(currentUserId, contactId);

                if (success)
                {
                    return Ok(new { message = "مخاطب حذف شد" });
                }

                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing contact {ContactId}, user {UserId}", contactId, GetUserId());
                return ServerError("خطا در حذف مخاطب");
            }
        }

        [HttpPost("contacts/{contactId}/interaction")]
        public async Task<IActionResult> UpdateLastInteraction(long contactId)
        {
            try
            {
                var currentUserId = GetUserId();
                var success = await _userService.UpdateLastInteractionAsync(currentUserId, contactId);

                if (success)
                {
                    return Ok(new { message = "آخرین تعامل به‌روزرسانی شد" });
                }

                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last interaction for contact {ContactId}, user {UserId}", contactId, GetUserId());
                return ServerError("خطا در به‌روزرسانی آخرین تعامل");
            }
        }

        [HttpGet("contacts/statistics")]
        public async Task<IActionResult> GetContactsStatistics()
        {
            try
            {
                var currentUserId = GetUserId();
                var totalContacts = await _userService.GetContactsCountAsync(currentUserId);
                var favoriteContacts = await _userService.GetFavoriteContactsCountAsync(currentUserId);
                var blockedContacts = await _userService.GetBlockedContactsCountAsync(currentUserId);

                var statistics = new
                {
                    totalContacts,
                    favoriteContacts,
                    blockedContacts,
                    unreadMessages = 0, // اینو بعداً از ChatService بگیریم
                    onlineContacts = 0   // اینو بعداً محاسبه کنیم
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contacts statistics for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت آمار مخاطبین");
            }
        }

        [HttpGet("contacts/filtered")]
        public async Task<IActionResult> GetFilteredContacts(
            [FromQuery] bool? isFavorite,
            [FromQuery] bool? isBlocked,
            [FromQuery] bool? hasChat,
            [FromQuery] string sortBy = "name",
            [FromQuery] string sortDirection = "asc")
        {
            try
            {
                var currentUserId = GetUserId();
                var contacts = await _userService.GetFilteredContactsAsync(
                    currentUserId,
                    isFavorite,
                    isBlocked,
                    hasChat,
                    sortBy,
                    sortDirection);

                return Ok(contacts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered contacts for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین فیلتر شده");
            }
        }

        [HttpPatch("contacts/batch")]
        public async Task<IActionResult> BatchUpdateContacts([FromBody] BatchUpdateContactsDto batchDto)
        {
            try
            {
                var currentUserId = GetUserId();
                var success = await _userService.BatchUpdateContactsAsync(
                    currentUserId,
                    batchDto.ContactIds,
                    batchDto.Updates);

                if (success)
                {
                    return Ok(new { message = "مخاطبین با موفقیت به‌روزرسانی شدند" });
                }

                return BadRequest(new { message = "خطا در به‌روزرسانی مخاطبین" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch updating contacts for user {UserId}", GetUserId());
                return ServerError("خطا در به‌روزرسانی گروهی مخاطبین");
            }
        }

        [HttpGet("contacts/mutual/{userId}")]
        public async Task<IActionResult> GetMutualContacts(long userId)
        {
            try
            {
                var currentUserId = GetUserId();
                var mutualContacts = await _userService.GetMutualContactsAsync(currentUserId, userId);
                return Ok(mutualContacts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mutual contacts between {UserId1} and {UserId2}",
                    GetUserId(), userId);
                return ServerError("خطا در دریافت مخاطبین مشترک");
            }
        }

        [HttpPost("contacts/import")]
        public async Task<IActionResult> ImportContacts([FromBody] ImportContactsDto importDto)
        {
            try
            {
                var currentUserId = GetUserId();

                // تبدیل DTO به مدل مناسب
                var contactItems = importDto.Contacts.Select(c => new ImportContactItem
                {
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    PhoneNumber = c.PhoneNumber ?? string.Empty,
                    Email = c.Email,
                    DisplayName = c.DisplayName,
                    IsFavorite = c.IsFavorite
                }).ToList();

                var result = await _userService.ImportContactsAsync(currentUserId, contactItems);

                return Ok(new
                {
                    message = "مخاطبین با موفقیت وارد شدند",
                    importedCount = result.ImportedCount,
                    duplicateCount = result.DuplicateCount,
                    errorCount = result.ErrorCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing contacts for user {UserId}", GetUserId());
                return ServerError("خطا در وارد کردن مخاطبین");
            }
        }

        [HttpPost("contacts/export")]
        public async Task<IActionResult> ExportContacts([FromBody] ExportContactsDto exportDto)
        {
            try
            {
                var currentUserId = GetUserId();
                var exportData = await _userService.ExportContactsAsync(currentUserId, exportDto.Format);

                var fileName = $"contacts_export_{DateTime.Now:yyyyMMdd_HHmmss}.{exportDto.Format.ToLower()}";
                var contentType = exportDto.Format.ToLower() switch
                {
                    "csv" => "text/csv",
                    "json" => "application/json",
                    "vcf" => "text/vcard",
                    _ => "application/octet-stream"
                };

                return File(exportData, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting contacts for user {UserId}", GetUserId());
                return ServerError("خطا در خروجی گرفتن از مخاطبین");
            }
        }

        [HttpPost("contacts/sync-status")]
        public async Task<IActionResult> UpdateSyncStatus([FromBody] SyncStatusDto syncDto)
        {
            try
            {
                var currentUserId = GetUserId();
                await _userService.UpdateSyncStatusAsync(currentUserId, syncDto.LastSyncTime, syncDto.SyncedCount);

                return Ok(new { message = "وضعیت همگام‌سازی به‌روزرسانی شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync status for user {UserId}", GetUserId());
                return ServerError("خطا در به‌روزرسانی وضعیت همگام‌سازی");
            }
        }


        public class ImportContactsDto
        {
            public List<ImportContactItemDto> Contacts { get; set; } = new();

            public class ImportContactItemDto
            {
                public string? FirstName { get; set; }
                public string? LastName { get; set; }
                public string? PhoneNumber { get; set; }
                public string? Email { get; set; }
                public string? DisplayName { get; set; }
                public bool IsFavorite { get; set; }
            }
        }

        public class ExportContactsDto
        {
            [Required]
            public string Format { get; set; } = "csv"; // csv, json, vcf

            public bool IncludeBlocked { get; set; } = false;
            public bool OnlyFavorites { get; set; } = false;
        }

        public class SyncStatusDto
        {
            public DateTime LastSyncTime { get; set; }
            public int SyncedCount { get; set; }
        }

        public class BatchUpdateContactsDto
        {
            public List<long> ContactIds { get; set; } = new();
            public Dictionary<string, object> Updates { get; set; } = new();
        }

        public class FavoriteContactDto
        {
            [Required]
            public bool IsFavorite { get; set; }
        }

        public class BlockContactDto
        {
            [Required]
            public bool IsBlocked { get; set; }
        }

        public class UpdateDisplayNameDto
        {
            [MaxLength(100)]
            public string? DisplayName { get; set; }
        }

    }
}