using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADPasswordManager.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedOUs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagedOUs",
                table: "DelegationRules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedOUs",
                table: "DelegationRules");
        }
    }
}
