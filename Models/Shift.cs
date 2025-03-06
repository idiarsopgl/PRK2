using System;
using System.ComponentModel.DataAnnotations;

namespace ParkIRC.Models
{
    public class Shift
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama shift wajib diisi")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Waktu mulai wajib diisi")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Waktu selesai wajib diisi")]
        public TimeSpan EndTime { get; set; }

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedAt { get; set; }
    }
} 