using Organizacional.Models;

namespace Organizacional.Models.ViewModels
{
    public class DetalleDocumentoViewModel
    {
        public Documento Documento { get; set; }
        public List<Tarea> Tareas { get; set; } = new();
        public List<MaterialesPendiente> Materiales { get; set; } = new();
        public MaterialesPendiente NuevoMaterial { get; set; } = new();
        public List<MaterialesPendiente> MaterialesSolicitados { get; set; } = new();
        public List<MaterialesPendiente> MaterialesDejados { get; set; } = new();
        public Usuario UsuarioActual { get; set; } = new();
    }
}
