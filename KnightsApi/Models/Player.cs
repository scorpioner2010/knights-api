using System.Collections.Generic;

namespace KnightsApi.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsAdmin { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public int Mmr { get; set; } = 0;
        public int Coins { get; set; } = 0;
        public int Gold { get; set; } = 0;
        public int FreeXp { get; set; } = 0;
        
        public ICollection<UserWarrior> UserWarriors { get; set; } = new List<UserWarrior>();
    }
}