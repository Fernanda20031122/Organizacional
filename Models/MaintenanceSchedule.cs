namespace Organizacional.Models
{
    public class MaintenanceSchedule
    {
        public int Id { get; set; }

        public int DocumentoId { get; set; }

        public Documento Document { get; set; } = null!;

        public short Seq { get; set; }
        public DateTime? PlannedDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool Notified7d { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}