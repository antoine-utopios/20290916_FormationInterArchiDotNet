using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Textinord.Trame.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initiale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Libelle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Famille = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PrixBase = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RaisonSociale = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConditionTarifaire = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StocksArticle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CodeEntrepot = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Quantite = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocksArticle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StocksArticle_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Commandes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Statut = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    Metadonnees = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Commandes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LignesCommande",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferenceArticle = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Quantite = table.Column<int>(type: "INTEGER", nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RemisePourcent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    CommandeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesCommande", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesCommande_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LignesCommande_Commandes_CommandeId",
                        column: x => x.CommandeId,
                        principalTable: "Commandes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Famille",
                table: "Articles",
                column: "Famille");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Reference",
                table: "Articles",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Code",
                table: "Clients",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_ClientId",
                table: "Commandes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_Date",
                table: "Commandes",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_Numero",
                table: "Commandes",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Commandes_Statut",
                table: "Commandes",
                column: "Statut");

            migrationBuilder.CreateIndex(
                name: "IX_LignesCommande_ArticleId",
                table: "LignesCommande",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesCommande_CommandeId",
                table: "LignesCommande",
                column: "CommandeId");

            migrationBuilder.CreateIndex(
                name: "IX_StocksArticle_ArticleId_CodeEntrepot",
                table: "StocksArticle",
                columns: new[] { "ArticleId", "CodeEntrepot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LignesCommande");

            migrationBuilder.DropTable(
                name: "StocksArticle");

            migrationBuilder.DropTable(
                name: "Commandes");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
