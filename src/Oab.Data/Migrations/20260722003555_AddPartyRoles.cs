using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oab.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Roles",
                table: "Parties",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Parties");
        }
    }
}
