using Microsoft.EntityFrameworkCore;

namespace TestTask;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }

    public DbSet<TaskItem> TaskItems { get; set; }
}