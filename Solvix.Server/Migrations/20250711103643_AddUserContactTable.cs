using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solvix.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserContactTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserContact",
                columns: table => new
                {
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: false),
                    ContactUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContact", x => new { x.OwnerUserId, x.ContactUserId });
                    table.ForeignKey(
                        name: "FK_UserContact_Users_ContactUserId",
                        column: x => x.ContactUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserContact_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserContact_ContactUserId",
                table: "UserContact",
                column: "ContactUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserContact");
        }
    }
}
