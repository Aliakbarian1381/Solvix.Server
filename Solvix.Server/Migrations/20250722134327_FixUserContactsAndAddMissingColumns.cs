using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solvix.Server.Migrations
{
    /// <inheritdoc />
    public partial class FixUserContactsAndAddMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix for UserContacts table
            // 1. Drop the old composite primary key
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserContacts",
                table: "UserContacts");

            // 2. Make the Id column the new primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_UserContacts",
                table: "UserContacts",
                column: "Id");

            // 3. Add a unique index to what was the old primary key
            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_OwnerUserId_ContactUserId",
                table: "UserContacts",
                columns: new[] { "OwnerUserId", "ContactUserId" },
                unique: true);

            // Add the missing IsOnline column to the Users table
            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the changes in reverse order
            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserContacts",
                table: "UserContacts");

            migrationBuilder.DropIndex(
                name: "IX_UserContacts_OwnerUserId_ContactUserId",
                table: "UserContacts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserContacts",
                table: "UserContacts",
                columns: new[] { "OwnerUserId", "ContactUserId" });
        }
    }
}