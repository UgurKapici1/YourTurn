using Microsoft.EntityFrameworkCore;
using YourTurn.Web.Data;
using YourTurn.Web.Interfaces;
using YourTurn.Web.Models;

namespace YourTurn.Web.Repositories
{
    // EF Core tabanlı oyun repository'si
    public class EfGameRepository : IGameRepository
    {
        private readonly YourTurnDbContext _context;

        public EfGameRepository(YourTurnDbContext context)
        {
            _context = context;
        }

        public Task<Category?> GetCategoryWithQuestionsAsync(string categoryName)
        {
            return _context.Categories
                .Include(c => c.Questions)
                .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(c => c.Name == categoryName);
        }
    }
}

