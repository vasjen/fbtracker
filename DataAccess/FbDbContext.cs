
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace fbtracker{
    public class FbDbContext: DbContext {
     private readonly IConfiguration _config;
     public DbSet<Card>? Cards {get;set;}
     public DbSet<Profit>? Profits {get;set;}   
    
    
    public FbDbContext(DbContextOptions<FbDbContext> options, IConfiguration config)
        : base(options)
    {
        _config = config;
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var conStrBuilder = new NpgsqlConnectionStringBuilder(
            _config.GetConnectionString("FbConnection"));
            conStrBuilder.Password = _config.GetValue<string>("DbPassword");
            var connection = conStrBuilder.ConnectionString;
            optionsBuilder.UseNpgsql(connection);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {  
    }
    }
}