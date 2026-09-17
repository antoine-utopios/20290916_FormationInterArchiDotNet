using Microsoft.EntityFrameworkCore;
using Project5.AjoutEFCore.Models;

namespace Project5.AjoutEFCore.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<Dog> Dogs { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {  }

    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // {
    //     optionsBuilder.UseSqlite("Data Source = kennel.db");
    // }
}