using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductShop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WebOrderPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaymentAmount",
                table: "WebOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentFrom",
                table: "WebOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "WebOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "COD");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "WebOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTrxId",
                table: "WebOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebOrders_PaymentTrxId",
                table: "WebOrders",
                column: "PaymentTrxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebOrders_PaymentTrxId",
                table: "WebOrders");

            migrationBuilder.DropColumn(
                name: "PaymentAmount",
                table: "WebOrders");

            migrationBuilder.DropColumn(
                name: "PaymentFrom",
                table: "WebOrders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "WebOrders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "WebOrders");

            migrationBuilder.DropColumn(
                name: "PaymentTrxId",
                table: "WebOrders");
        }
    }
}
