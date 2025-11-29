using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class UseExistingAccountTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Skip creating the Accounts table as it already exists
            // Just ensure we have the email index
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Accounts_email' AND object_id = OBJECT_ID('Accounts'))
                BEGIN
                    CREATE UNIQUE INDEX IX_Accounts_email ON Accounts(email);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Don't drop the Accounts table, just remove the index if needed
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Accounts_email' AND object_id = OBJECT_ID('Accounts'))
                BEGIN
                    DROP INDEX IX_Accounts_email ON Accounts;
                END
            ");
        }
    }
}
