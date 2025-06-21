using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    public class Question
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Soru metni zorunludur.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Soru metni 5 ile 500 karakter arasında olmalıdır.")]
        public string Text { get; set; }

        [Required(ErrorMessage = "Kategori ID zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçilmelidir.")]
        public int CategoryId { get; set; }

        public Category? Category { get; set; }
        
        public List<Answer> Answers { get; set; } = new List<Answer>();
    }
} 