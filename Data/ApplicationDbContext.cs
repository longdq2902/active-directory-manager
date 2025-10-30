using ADPasswordManager.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ADPasswordManager.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ADPasswordManager.Models.Entities.DelegationRule> DelegationRules { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<OuSqlInstanceMapping> OuSqlInstanceMappings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);


            // --- THÊM DÒNG NÀY ĐỂ TẠO UNIQUE INDEX ---
            builder.Entity<OuSqlInstanceMapping>()
                   .HasIndex(m => m.OuDistinguishedName)
                   .IsUnique();
            // ----------------------------------------

        }

    }

}
