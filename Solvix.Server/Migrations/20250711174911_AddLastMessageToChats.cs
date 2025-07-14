using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solvix.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLastMessageToChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserContact_Users_ContactUserId",
                table: "UserContact");

            migrationBuilder.DropForeignKey(
                name: "FK_UserContact_Users_OwnerUserId",
                table: "UserContact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserContact",
                table: "UserContact");

            migrationBuilder.RenameTable(
                name: "UserContact",
                newName: "UserContacts");

            migrationBuilder.RenameIndex(
                name: "IX_UserContact_ContactUserId",
                table: "UserContacts",
                newName: "IX_UserContacts_ContactUserId");

            migrationBuilder.AddColumn<string>(
                name: "LastMessage",
                table: "Chats",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageTime",
                table: "Chats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnreadCount",
                table: "Chats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserContacts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "UserContacts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "UserContacts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "UserContacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "UserContacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInteractionAt",
                table: "UserContacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserContacts",
                table: "UserContacts",
                columns: new[] { "OwnerUserId", "ContactUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserContacts_Users_ContactUserId",
                table: "UserContacts",
                column: "ContactUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserContacts_Users_OwnerUserId",
                table: "UserContacts",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserContacts_Users_ContactUserId",
                table: "UserContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserContacts_Users_OwnerUserId",
                table: "UserContacts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserContacts",
                table: "UserContacts");

            migrationBuilder.DropColumn(
                name: "LastMessage",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "LastMessageTime",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "UnreadCount",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserContacts");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "UserContacts");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserContacts");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "UserContacts");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "UserContacts");

            migrationBuilder.DropColumn(
                name: "LastInteractionAt",
                table: "UserContacts");

            migrationBuilder.RenameTable(
                name: "UserContacts",
                newName: "UserContact");

            migrationBuilder.RenameIndex(
                name: "IX_UserContacts_ContactUserId",
                table: "UserContact",
                newName: "IX_UserContact_ContactUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserContact",
                table: "UserContact",
                columns: new[] { "OwnerUserId", "ContactUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserContact_Users_ContactUserId",
                table: "UserContact",
                column: "ContactUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserContact_Users_OwnerUserId",
                table: "UserContact",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
