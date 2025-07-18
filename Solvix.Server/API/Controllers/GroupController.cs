using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class GroupController : BaseController
    {
        private readonly IGroupManagementService _groupManagementService;
        private readonly IChatService _chatService;

        public GroupController(
            IGroupManagementService groupManagementService,
            IChatService chatService,
            ILogger<GroupController> logger) : base(logger)
        {
            _groupManagementService = groupManagementService;
            _chatService = chatService;
        }

        [HttpGet("{chatId}/info")]
        public async Task<IActionResult> GetGroupInfo(Guid chatId)
        {
            try
            {
                long userId = GetUserId();
                var groupInfo = await _groupManagementService.GetGroupInfoAsync(chatId, userId);

                if (groupInfo == null)
                {
                    return NotFound("گروه یافت نشد یا شما عضو این گروه نیستید");
                }

                return Ok(groupInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group info for {ChatId}", chatId);
                return ServerError("خطا در دریافت اطلاعات گروه");
            }
        }

        [HttpPut("{chatId}/info")]
        public async Task<IActionResult> UpdateGroupInfo(Guid chatId, [FromBody] UpdateGroupDto dto)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.UpdateGroupInfoAsync(chatId, userId, dto);

                if (!result)
                {
                    return Forbidden("شما دسترسی ویرایش اطلاعات گروه را ندارید");
                }

                return Ok(new { message = "اطلاعات گروه با موفقیت به‌روزرسانی شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group info for {ChatId}", chatId);
                return ServerError("خطا در به‌روزرسانی اطلاعات گروه");
            }
        }

        [HttpGet("{chatId}/settings")]
        public async Task<IActionResult> GetGroupSettings(Guid chatId)
        {
            try
            {
                long userId = GetUserId();
                var settings = await _chatService.GetGroupSettingsAsync(chatId, userId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group settings for {ChatId}", chatId);
                return ServerError("خطا در دریافت تنظیمات گروه");
            }
        }

        [HttpPut("{chatId}/settings")]
        public async Task<IActionResult> UpdateGroupSettings(Guid chatId, [FromBody] GroupSettingsDto settings)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.UpdateGroupSettingsAsync(chatId, userId, settings);

                if (!result)
                {
                    return Forbidden("شما دسترسی تغییر تنظیمات گروه را ندارید");
                }

                return Ok(new { message = "تنظیمات گروه با موفقیت به‌روزرسانی شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group settings for {ChatId}", chatId);
                return ServerError("خطا در به‌روزرسانی تنظیمات گروه");
            }
        }

        [HttpGet("{chatId}/members")]
        public async Task<IActionResult> GetGroupMembers(Guid chatId)
        {
            try
            {
                long userId = GetUserId();
                var members = await _groupManagementService.GetGroupMembersAsync(chatId, userId);
                return Ok(members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group members for {ChatId}", chatId);
                return ServerError("خطا در دریافت لیست اعضای گروه");
            }
        }

        [HttpPost("{chatId}/members")]
        public async Task<IActionResult> AddMembers(Guid chatId, [FromBody] AddMembersDto dto)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.AddMembersAsync(chatId, userId, dto.UserIds);

                if (!result)
                {
                    return Forbidden("شما دسترسی اضافه کردن عضو ندارید");
                }

                return Ok(new { message = "اعضا با موفقیت اضافه شدند" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding members to group {ChatId}", chatId);
                return ServerError("خطا در اضافه کردن اعضا");
            }
        }

        [HttpDelete("{chatId}/members/{memberId}")]
        public async Task<IActionResult> RemoveMember(Guid chatId, long memberId)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.RemoveMemberAsync(chatId, userId, memberId);

                if (!result)
                {
                    return Forbidden("شما دسترسی حذف عضو ندارید");
                }

                return Ok(new { message = "عضو با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {MemberId} from group {ChatId}", memberId, chatId);
                return ServerError("خطا در حذف عضو");
            }
        }

        [HttpPut("{chatId}/members/{memberId}/role")]
        public async Task<IActionResult> UpdateMemberRole(Guid chatId, long memberId, [FromBody] UpdateMemberRoleDto dto)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.UpdateMemberRoleAsync(chatId, userId, memberId, dto.NewRole);

                if (!result)
                {
                    return Forbidden("شما دسترسی تغییر نقش عضو ندارید");
                }

                return Ok(new { message = "نقش عضو با موفقیت تغییر کرد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member role for {MemberId} in group {ChatId}", memberId, chatId);
                return ServerError("خطا در تغییر نقش عضو");
            }
        }

        [HttpPost("{chatId}/leave")]
        public async Task<IActionResult> LeaveGroup(Guid chatId)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.LeaveGroupAsync(chatId, userId);

                if (!result)
                {
                    return BadRequest("خطا در خروج از گروه");
                }

                return Ok(new { message = "شما با موفقیت از گروه خارج شدید" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving group {ChatId} for user {UserId}", chatId, GetUserId());
                return ServerError("خطا در خروج از گروه");
            }
        }

        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteGroup(Guid chatId)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.DeleteGroupAsync(chatId, userId);

                if (!result)
                {
                    return Forbidden("فقط مالک گروه می‌تواند آن را حذف کند");
                }

                return Ok(new { message = "گروه با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {ChatId} by user {UserId}", chatId, GetUserId());
                return ServerError("خطا در حذف گروه");
            }
        }

        [HttpPost("{chatId}/transfer-ownership")]
        public async Task<IActionResult> TransferOwnership(Guid chatId, [FromBody] TransferOwnershipDto dto)
        {
            try
            {
                long userId = GetUserId();
                var result = await _groupManagementService.TransferOwnershipAsync(chatId, userId, dto.NewOwnerId);

                if (!result)
                {
                    return Forbidden("فقط مالک گروه می‌تواند مالکیت را منتقل کند");
                }

                return Ok(new { message = "مالکیت گروه با موفقیت منتقل شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring ownership of group {ChatId}", chatId);
                return ServerError("خطا در انتقال مالکیت گروه");
            }
        }
    }
}