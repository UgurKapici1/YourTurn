using YourTurn.Web.Models;

namespace YourTurn.Web.Interfaces
{
    // Oyun verileri için repository (SRP, DIP)
    public interface IGameRepository
    {
        Task<Category?> GetCategoryWithQuestionsAsync(string categoryName);
    }
}

