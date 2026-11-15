using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Data.Migrations
{
    // migrationBuilder.DropTable("Customers") in a comment is not code.
    /// <inheritdoc />
    public partial class UnrelatedMatchingNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same method names, different receiver.
            var archive = new ArchiveClient();
            archive.DropTable("Customers");
            archive.DropColumn("Bio", "Customers");
            archive.AddColumn("TenantId", "Orders", nullable: false);
            archive.Sql("DELETE FROM Customers;");

            // A private helper with a matching name, called without a receiver.
            DropColumn("Bio", "Customers");

            // Text that looks like an operation.
            var note = "migrationBuilder.DropTable(\"Orders\")";
            archive.Log(note);

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Notes");
        }

        private static void DropColumn(string name, string table)
        {
        }
    }

    /// <summary>Methods named Up that do not take a MigrationBuilder are not migrations.</summary>
    public class SeedSteps
    {
        public void Up(ArchiveClient archive) => archive.DropTable("Customers");

        public void Up(ModelBuilder modelBuilder, ArchiveClient archive) => archive.DropTable("Orders");
    }

    public class ArchiveClient
    {
        public void DropTable(string name) { }

        public void DropColumn(string name, string table) { }

        public void AddColumn(string name, string table, bool nullable) { }

        public void Sql(string sql) { }

        public void Log(string message) { }
    }
}
