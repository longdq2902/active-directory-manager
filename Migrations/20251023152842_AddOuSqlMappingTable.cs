using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADPasswordManager.Migrations
{
    /// <inheritdoc />
    public partial class AddOuSqlMappingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OuSqlInstanceMappings",
                columns: table => new
                {
                    OuDistinguishedName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SqlInstanceName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuSqlInstanceMappings", x => x.OuDistinguishedName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OuSqlInstanceMappings");
        }
    }
}
