using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateTemplateIdToMicroMajorCertificate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CertificateTemplateId",
                table: "AppMicroMajorCertificates",
                type: "uuid",
                nullable: true);

            // 回填历史证书的模板关联。
            // 背景：占位符图层功能上线前发的证书，CertificateImageUrl 就是模板原图 URL
            //（未叠加任何占位符文字），因此可与同微专业的模板按 ImageUrl 精确匹配。
            // 回填后这些历史证书也能按模板当前图层动态渲染，无需重新发证。
            // 仅匹配同一微专业下的模板，避免跨微专业图片 URL 碰撞导致误关联。
            migrationBuilder.Sql(
                "UPDATE \"AppMicroMajorCertificates\" AS c " +
                "SET \"CertificateTemplateId\" = t.\"Id\" " +
                "FROM \"AppMicroMajorCertificateTemplates\" AS t " +
                "WHERE c.\"CertificateTemplateId\" IS NULL " +
                "AND c.\"CertificateImageUrl\" IS NOT NULL " +
                "AND t.\"ImageUrl\" = c.\"CertificateImageUrl\" " +
                "AND t.\"MicroMajorId\" = c.\"MicroMajorId\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateTemplateId",
                table: "AppMicroMajorCertificates");
        }
    }
}
