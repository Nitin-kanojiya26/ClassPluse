using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassPluse.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceRecognitionProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaceEncoding",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasRegisteredFace",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaceEncoding",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HasRegisteredFace",
                table: "AspNetUsers");
        }
    }
}
