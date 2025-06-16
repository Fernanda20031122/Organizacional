using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Organizacional.Models;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace Organizacional.Data;

public partial class OrganizacionalContext : DbContext
{
    public OrganizacionalContext()
    {
    }

    public OrganizacionalContext(DbContextOptions<OrganizacionalContext> options)
        : base(options)
    {
    }


    public virtual DbSet<Documento> Documentos { get; set; }

    public virtual DbSet<Historial> Historials { get; set; }

    public virtual DbSet<Mantenimiento> Mantenimientos { get; set; }

    public virtual DbSet<Notificacione> Notificaciones { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SedesDocumento> SedesDocumentos { get; set; }

    public virtual DbSet<Tarea> Tareas { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
            //   => optionsBuilder.UseMySql("server=127.0.0.1;port=3306;database=organizacional;uid=root;password=6020", Microsoft.EntityFrameworkCore.ServerVersion.Parse("10.4.32-mariadb"));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");


        modelBuilder.Entity<Documento>(entity =>
        {
            entity.HasKey(e => e.IdDocumento).HasName("PRIMARY");

            entity.ToTable("documentos");

            entity.HasIndex(e => e.IdUsuarioSubio, "id_usuario_subio");

            entity.Property(e => e.IdDocumento)
                .HasColumnType("int(11)")
                .HasColumnName("id_documento");
            entity.Property(e => e.ArchivoUrl)
                .HasMaxLength(255)
                .HasColumnName("archivo_url");
            entity.Property(e => e.Asignada)
                .HasDefaultValueSql("'0'")
                .HasColumnName("asignada");
            entity.Property(e => e.CotizacionArchivoUrl)
                .HasMaxLength(255)
                .HasColumnName("cotizacion_archivo_url");
            entity.Property(e => e.CotizacionFecha)
                .HasColumnType("datetime")
                .HasColumnName("cotizacion_fecha");
            entity.Property(e => e.Descripcion)
                .HasColumnType("text")
                .HasColumnName("descripcion");
            entity.Property(e => e.EmpresaDestino)
                .HasMaxLength(100)
                .HasColumnName("empresa_destino");
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin");
            entity.Property(e => e.FechaGeneracion).HasColumnName("fecha_generacion");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.FechaSubida).HasColumnName("fecha_subida");
            entity.Property(e => e.IdUsuarioSubio)
                .HasColumnType("int(11)")
                .HasColumnName("id_usuario_subio");
            entity.Property(e => e.Instalacion)
                .HasDefaultValueSql("'0'")
                .HasColumnName("instalacion");
            entity.Property(e => e.Mantenimiento)
                .HasDefaultValueSql("'0'")
                .HasColumnName("mantenimiento");
            entity.Property(e => e.NumeroDocumento)
                .HasMaxLength(100)
                .HasColumnName("numero_documento");
            entity.Property(e => e.Suministro)
                .HasDefaultValueSql("'0'")
                .HasColumnName("suministro");
            entity.Property(e => e.TipoDocumento)
                .HasColumnType("enum('Orden','Contrato')")
                .HasColumnName("tipo_documento");

            entity.HasOne(d => d.IdUsuarioSubioNavigation).WithMany(p => p.Documentos)
                .HasForeignKey(d => d.IdUsuarioSubio)
                .HasConstraintName("documentos_ibfk_1");
        });

        modelBuilder.Entity<Historial>(entity =>
        {
            entity.HasKey(e => e.IdHistorial).HasName("PRIMARY");

            entity.ToTable("historial");

            entity.HasIndex(e => e.IdDocumento, "id_documento");

            entity.HasIndex(e => e.IdUsuario, "id_usuario");

            entity.Property(e => e.IdHistorial)
                .HasColumnType("int(11)")
                .HasColumnName("id_historial");
            entity.Property(e => e.Accion)
                .HasMaxLength(255)
                .HasColumnName("accion");
            entity.Property(e => e.Fecha)
                .HasColumnType("datetime")
                .HasColumnName("fecha");
            entity.Property(e => e.IdDocumento)
                .HasColumnType("int(11)")
                .HasColumnName("id_documento");
            entity.Property(e => e.IdUsuario)
                .HasColumnType("int(11)")
                .HasColumnName("id_usuario");

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.Historials)
                .HasForeignKey(d => d.IdDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("historial_ibfk_2");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Historials)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("historial_ibfk_1");
        });

        modelBuilder.Entity<Mantenimiento>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("mantenimientos");

            entity.HasIndex(e => e.IdDocumento, "id_documento");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.IdDocumento)
                .HasColumnType("int(11)")
                .HasColumnName("id_documento");
            entity.Property(e => e.MantenimientoRealizado)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("mantenimiento_realizado");
            entity.Property(e => e.ProximoMantenimiento).HasColumnName("proximo_mantenimiento");
            entity.Property(e => e.TotalMantenimientos)
                .HasColumnType("int(11)")
                .HasColumnName("total_mantenimientos");
            entity.Property(e => e.FechasRealizadasJson)
                .HasColumnType("longtext")
                .HasColumnName("fechas_realizadasJson");

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.Mantenimientos)
                .HasForeignKey(d => d.IdDocumento)
                .HasConstraintName("mantenimientos_ibfk_1");
        });

        modelBuilder.Entity<Notificacione>(entity =>
        {
            entity.HasKey(e => e.IdNotificacion).HasName("PRIMARY");

            entity.ToTable("notificaciones");

            entity.HasIndex(e => e.IdUsuario, "id_usuario");

            entity.Property(e => e.IdNotificacion)
                .HasColumnType("int(11)")
                .HasColumnName("id_notificacion");
            entity.Property(e => e.Fecha)
                .HasColumnType("datetime")
                .HasColumnName("fecha");
            entity.Property(e => e.IdUsuario)
                .HasColumnType("int(11)")
                .HasColumnName("id_usuario");
            entity.Property(e => e.Leida)
                .HasDefaultValueSql("'0'")
                .HasColumnName("leida");
            entity.Property(e => e.Mensaje)
                .HasColumnType("text")
                .HasColumnName("mensaje");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Notificaciones)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notificaciones_ibfk_1");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.IdRol).HasName("PRIMARY");

            entity.ToTable("roles");

            entity.Property(e => e.IdRol)
                .HasColumnType("int(11)")
                .HasColumnName("id_rol");
            entity.Property(e => e.NombreRol)
                .HasMaxLength(50)
                .HasColumnName("nombre_rol");
        });

        modelBuilder.Entity<SedesDocumento>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("sedes_documento");

            entity.HasIndex(e => e.IdDocumento, "id_documento");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Direccion)
                .HasMaxLength(255)
                .HasColumnName("direccion");
            entity.Property(e => e.IdDocumento)
                .HasColumnType("int(11)")
                .HasColumnName("id_documento");

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.SedesDocumentos)
                .HasForeignKey(d => d.IdDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("sedes_documento_ibfk_1");
        });

        modelBuilder.Entity<Tarea>(entity =>
        {
            entity.HasKey(e => e.IdTarea).HasName("PRIMARY");

            entity.ToTable("tareas");

            entity.HasIndex(e => e.IdDocumento, "id_documento");

            entity.HasIndex(e => e.IdTecnicoAsignado, "id_tecnico_asignado");

            entity.Property(e => e.IdTarea)
                .HasColumnType("int(11)")
                .HasColumnName("id_tarea");
            entity.Property(e => e.Estado)
                .HasDefaultValueSql("'pendiente'")
                .HasColumnType("enum('pendiente','en_progreso','completado')")
                .HasColumnName("estado");
            entity.Property(e => e.FechaAsignacion).HasColumnName("fecha_asignacion");
            entity.Property(e => e.FechaEjecucion).HasColumnName("fecha_ejecucion");
            entity.Property(e => e.IdDocumento)
                .HasColumnType("int(11)")
                .HasColumnName("id_documento");
            entity.Property(e => e.IdTecnicoAsignado)
                .HasColumnType("int(11)")
                .HasColumnName("id_tecnico_asignado");

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.Tareas)
                .HasForeignKey(d => d.IdDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tareas_ibfk_1");

            entity.HasOne(d => d.IdTecnicoAsignadoNavigation).WithMany(p => p.Tareas)
                .HasForeignKey(d => d.IdTecnicoAsignado)
                .HasConstraintName("tareas_ibfk_2");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PRIMARY");

            entity.ToTable("usuarios");

            entity.HasIndex(e => e.IdRol, "id_rol");

            entity.Property(e => e.IdUsuario)
                .HasColumnType("int(11)")
                .HasColumnName("id_usuario");
            entity.Property(e => e.Contrasena)
                .HasMaxLength(255)
                .HasColumnName("contrasena");
            entity.Property(e => e.Correo)
                .HasMaxLength(100)
                .HasColumnName("correo");
            entity.Property(e => e.Estado)
                .HasDefaultValueSql("'activo'")
                .HasColumnType("enum('activo','inactivo')")
                .HasColumnName("estado");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("fecha_creacion");
            entity.Property(e => e.IdRol)
                .HasColumnType("int(11)")
                .HasColumnName("id_rol");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");

            entity.HasOne(d => d.IdRolNavigation).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.IdRol)
                .HasConstraintName("usuarios_ibfk_1");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
