using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class MakeMicroMajorCertificateIndexesFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppMicroMajorCertificates_CertificateNo",
                table: "AppMicroMajorCertificates");

            migrationBuilder.DropIndex(
                name: "IX_AppMicroMajorCertificates_EnrollmentId",
                table: "AppMicroMajorCertificates");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_CertificateNo",
                table: "AppMicroMajorCertificates",
                column: "CertificateNo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_EnrollmentId",
                table: "AppMicroMajorCertificates",
                column: "EnrollmentId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppMicroMajorCertificates_CertificateNo",
                table: "AppMicroMajorCertificates");

            migrationBuilder.DropIndex(
                name: "IX_AppMicroMajorCertificates_EnrollmentId",
                table: "AppMicroMajorCertificates");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_CertificateNo",
                table: "AppMicroMajorCertificates",
                column: "CertificateNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_EnrollmentId",
                table: "AppMicroMajorCertificates",
                column: "EnrollmentId",
                unique: true);
        }
    }
}
