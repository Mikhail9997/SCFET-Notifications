using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addDeviceTokenAndPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeviceToken",
                table: "Users",
                newName: "TelegramId");
            
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeviceToken",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_Users_PhoneNumber"" 
              ON ""Users"" (""PhoneNumber"") 
              WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" != '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users");
        
            // Удаляем колонки
            migrationBuilder.DropColumn(
                name: "DeviceToken",
                table: "Users");
        
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Users");
            
            migrationBuilder.RenameColumn(
                name: "TelegramId",
                table: "Users",
                newName: "DeviceToken");
        }
    }
}
