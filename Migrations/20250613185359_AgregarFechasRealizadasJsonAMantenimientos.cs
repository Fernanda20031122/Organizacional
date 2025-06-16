using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Organizacional.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFechasRealizadasJsonAMantenimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id_rol = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre_rol = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_rol);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id_usuario = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    correo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    contrasena = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    id_rol = table.Column<int>(type: "int(11)", nullable: true),
                    estado = table.Column<string>(type: "enum('activo','inactivo')", nullable: true, defaultValueSql: "'activo'", collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_creacion = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "current_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_usuario);
                    table.ForeignKey(
                        name: "usuarios_ibfk_1",
                        column: x => x.id_rol,
                        principalTable: "roles",
                        principalColumn: "id_rol");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    id_documento = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tipo_documento = table.Column<string>(type: "enum('Orden','Contrato')", nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    numero_documento = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_generacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_subida = table.Column<DateOnly>(type: "date", nullable: true),
                    archivo_url = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    id_usuario_subio = table.Column<int>(type: "int(11)", nullable: true),
                    asignada = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    empresa_destino = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    suministro = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    instalacion = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    mantenimiento = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_documento);
                    table.ForeignKey(
                        name: "documentos_ibfk_1",
                        column: x => x.id_usuario_subio,
                        principalTable: "usuarios",
                        principalColumn: "id_usuario");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    id_notificacion = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_usuario = table.Column<int>(type: "int(11)", nullable: false),
                    mensaje = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    leida = table.Column<bool>(type: "tinyint(1)", nullable: true, defaultValueSql: "'0'"),
                    fecha = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_notificacion);
                    table.ForeignKey(
                        name: "notificaciones_ibfk_1",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id_usuario");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "cotizaciones",
                columns: table => new
                {
                    id_cotizacion = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_documento = table.Column<int>(type: "int(11)", nullable: false),
                    archivo_url = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_subida = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_cotizacion);
                    table.ForeignKey(
                        name: "cotizaciones_ibfk_1",
                        column: x => x.id_documento,
                        principalTable: "documentos",
                        principalColumn: "id_documento");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "historial",
                columns: table => new
                {
                    id_historial = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_usuario = table.Column<int>(type: "int(11)", nullable: false),
                    accion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    id_documento = table.Column<int>(type: "int(11)", nullable: false),
                    fecha = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_historial);
                    table.ForeignKey(
                        name: "historial_ibfk_1",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id_usuario");
                    table.ForeignKey(
                        name: "historial_ibfk_2",
                        column: x => x.id_documento,
                        principalTable: "documentos",
                        principalColumn: "id_documento");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "mantenimientos",
                columns: table => new
                {
                    id = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_documento = table.Column<int>(type: "int(11)", nullable: true),
                    total_mantenimientos = table.Column<int>(type: "int(11)", nullable: true),
                    mantenimiento_realizado = table.Column<int>(type: "int(11)", nullable: true, defaultValueSql: "'0'"),
                    proximo_mantenimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    FechasRealizadasJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id);
                    table.ForeignKey(
                        name: "mantenimientos_ibfk_1",
                        column: x => x.id_documento,
                        principalTable: "documentos",
                        principalColumn: "id_documento");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "sedes_documento",
                columns: table => new
                {
                    id = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_documento = table.Column<int>(type: "int(11)", nullable: false),
                    direccion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id);
                    table.ForeignKey(
                        name: "sedes_documento_ibfk_1",
                        column: x => x.id_documento,
                        principalTable: "documentos",
                        principalColumn: "id_documento");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "tareas",
                columns: table => new
                {
                    id_tarea = table.Column<int>(type: "int(11)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_documento = table.Column<int>(type: "int(11)", nullable: false),
                    id_tecnico_asignado = table.Column<int>(type: "int(11)", nullable: true),
                    fecha_asignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_ejecucion = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "enum('pendiente','en_progreso','completado')", nullable: true, defaultValueSql: "'pendiente'", collation: "utf8mb4_general_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Completada = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.id_tarea);
                    table.ForeignKey(
                        name: "tareas_ibfk_1",
                        column: x => x.id_documento,
                        principalTable: "documentos",
                        principalColumn: "id_documento");
                    table.ForeignKey(
                        name: "tareas_ibfk_2",
                        column: x => x.id_tecnico_asignado,
                        principalTable: "usuarios",
                        principalColumn: "id_usuario");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateIndex(
                name: "id_documento",
                table: "cotizaciones",
                column: "id_documento");

            migrationBuilder.CreateIndex(
                name: "id_usuario_subio",
                table: "documentos",
                column: "id_usuario_subio");

            migrationBuilder.CreateIndex(
                name: "id_documento1",
                table: "historial",
                column: "id_documento");

            migrationBuilder.CreateIndex(
                name: "id_usuario",
                table: "historial",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "id_documento2",
                table: "mantenimientos",
                column: "id_documento");

            migrationBuilder.CreateIndex(
                name: "id_usuario1",
                table: "notificaciones",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "id_documento3",
                table: "sedes_documento",
                column: "id_documento");

            migrationBuilder.CreateIndex(
                name: "id_documento4",
                table: "tareas",
                column: "id_documento");

            migrationBuilder.CreateIndex(
                name: "id_tecnico_asignado",
                table: "tareas",
                column: "id_tecnico_asignado");

            migrationBuilder.CreateIndex(
                name: "id_rol",
                table: "usuarios",
                column: "id_rol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cotizaciones");

            migrationBuilder.DropTable(
                name: "historial");

            migrationBuilder.DropTable(
                name: "mantenimientos");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "sedes_documento");

            migrationBuilder.DropTable(
                name: "tareas");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
