using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace MarqSpec.AgentForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "guideline_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Source = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guideline_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingested_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OpenEmrDocumentReferenceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingested_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IngestedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guideline_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuidelineDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Content" })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guideline_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guideline_chunks_guideline_documents_GuidelineDocumentId",
                        column: x => x.GuidelineDocumentId,
                        principalTable: "guideline_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "derived_facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestedDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    citation_source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    citation_source_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    citation_page_or_section = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    citation_field_or_chunk_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    citation_quote_or_value = table.Column<string>(type: "text", nullable: true),
                    citation_bbox = table.Column<double[]>(type: "double precision[]", nullable: true),
                    ExtractionConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_derived_facts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_derived_facts_ingested_documents_IngestedDocumentId",
                        column: x => x.IngestedDocumentId,
                        principalTable: "ingested_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_derived_facts_IngestedDocumentId",
                table: "derived_facts",
                column: "IngestedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_guideline_chunks_Embedding",
                table: "guideline_chunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "IX_guideline_chunks_GuidelineDocumentId_Ordinal",
                table: "guideline_chunks",
                columns: new[] { "GuidelineDocumentId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_guideline_chunks_search_vector",
                table: "guideline_chunks",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_ingested_documents_ContentHash",
                table: "ingested_documents",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingested_documents_PatientId",
                table: "ingested_documents",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_CorrelationId",
                table: "ingestion_jobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_Status",
                table: "ingestion_jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "derived_facts");

            migrationBuilder.DropTable(
                name: "guideline_chunks");

            migrationBuilder.DropTable(
                name: "ingestion_jobs");

            migrationBuilder.DropTable(
                name: "ingested_documents");

            migrationBuilder.DropTable(
                name: "guideline_documents");
        }
    }
}
