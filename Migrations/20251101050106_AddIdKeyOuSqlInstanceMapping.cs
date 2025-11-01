using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADPasswordManager.Migrations
{
    /// <inheritdoc />
    public partial class AddIdKeyOuSqlInstanceMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OuSqlInstanceMappings",
                table: "OuSqlInstanceMappings");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "OuSqlInstanceMappings",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OuSqlInstanceMappings",
                table: "OuSqlInstanceMappings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_OuSqlInstanceMappings_OuDistinguishedName",
                table: "OuSqlInstanceMappings",
                column: "OuDistinguishedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OuSqlInstanceMappings",
                table: "OuSqlInstanceMappings");

            migrationBuilder.DropIndex(
                name: "IX_OuSqlInstanceMappings_OuDistinguishedName",
                table: "OuSqlInstanceMappings");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "OuSqlInstanceMappings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OuSqlInstanceMappings",
                table: "OuSqlInstanceMappings",
                column: "OuDistinguishedName");
        }
    }
}
