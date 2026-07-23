using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class FixAddMissingChapterExerciseDesigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AppChapterExercises 表已由 AddChapterExerciseJoinTable 迁移创建。
            // 此迁移为数据库快照差异生成，实际表已存在，无需再创建。
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
