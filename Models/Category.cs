using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    public class Category
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Kategori adı zorunludur.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Kategori adı 2 ile 100 karakter arasında olmalıdır.")]
        public string Name { get; set; }
        public List<Question> Questions { get; set; } = new List<Question>();
    }
} 