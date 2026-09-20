using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolApi.Migrations
{
    /// <inheritdoc />
    public partial class VinculoUnicoDeConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_AlunoId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ProfessorId",
                table: "Usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AlunoId",
                table: "Usuarios",
                column: "AlunoId",
                unique: true,
                filter: "\"AlunoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ProfessorId",
                table: "Usuarios",
                column: "ProfessorId",
                unique: true,
                filter: "\"ProfessorId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_AlunoId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_ProfessorId",
                table: "Usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AlunoId",
                table: "Usuarios",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ProfessorId",
                table: "Usuarios",
                column: "ProfessorId");
        }
    }
}
