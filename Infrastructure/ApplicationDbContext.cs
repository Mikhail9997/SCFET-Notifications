using Core.Models;
using Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationReceiver> NotificationReceivers => Set<NotificationReceiver>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new GroupConfiguration());
            modelBuilder.ApplyConfiguration(new NotificationConfiguration());
            modelBuilder.ApplyConfiguration(new NotificationReceiverConfiguration());

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Groups
            var groups = new[]
            {
                new Group { Id = Guid.NewGuid(), Name = "ИТ-21", Description = "Группа информационных технологий 2021" },
                new Group { Id = Guid.NewGuid(), Name = "БУ-21", Description = "Группа бухгалтеров 2021" },
                new Group { Id = Guid.NewGuid(), Name = "ИТ-22", Description = "Группа информационных технологий 2022" },
                new Group { Id = Guid.NewGuid(), Name = "Исип22/1", Description = "Группа информационных технологий 2022" }
            };

            modelBuilder.Entity<Group>().HasData(groups);

            // Seed Admin user (password: "admin123")
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@scfet.ru",
                PasswordHash = "10000.KtO5UPCwLgoZiPkTPcBFdg==.GnqlYRsAlmYbaoEHbrgC7ISjPuArcsKmYHgmJ0fPTy8=", // admin123
                FirstName = "Администратор",
                LastName = "Системы",
                Role = UserRole.Administrator,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            // Seed Admin user (password: "admin123")
            var admin2User = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin2@scfet.ru",
                PasswordHash = "10000.KtO5UPCwLgoZiPkTPcBFdg==.GnqlYRsAlmYbaoEHbrgC7ISjPuArcsKmYHgmJ0fPTy8=", // admin123
                FirstName = "Администратор2",
                LastName = "Системы",
                Role = UserRole.Administrator,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Seed Teacher user (password: "teacher123")
            var teacherUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "teacher@scfet.ru",
                PasswordHash = "10000.SR7TWPsSkx86MW5kqhsk9g==.fmFOgSZurlhe/T6sBgfgE7V9liHsKaCdybPEyXCXREA=", // teacher123
                FirstName = "Иван",
                LastName = "Преподаватель",
                Role = UserRole.Teacher,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            // Seed Teacher user (password: "teacher123")
            var teacher2User = new User
            {
                Id = Guid.NewGuid(),
                Email = "teacher2@scfet.ru",
                PasswordHash = "10000.SR7TWPsSkx86MW5kqhsk9g==.fmFOgSZurlhe/T6sBgfgE7V9liHsKaCdybPEyXCXREA=", // teacher123
                FirstName = "Алексей",
                LastName = "Преподаватель",
                Role = UserRole.Teacher,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            
            // Seed Student user (password:student123)
            var studentUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "student@scfet.ru",
                PasswordHash = "10000.ubXk5yxtgQgWcc3/M+w4eQ==.U2b8/891ganlfSu1GvNNjx8OvTjPz9DBwRl6jT/SPwM=", // student123
                FirstName = "Михаил",
                LastName = "Студент",
                Role = UserRole.Student,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            // Seed Student user (password:student123)
            var student2User = new User
            {
                Id = Guid.NewGuid(),
                Email = "student2@scfet.ru",
                PasswordHash = "10000.ubXk5yxtgQgWcc3/M+w4eQ==.U2b8/891ganlfSu1GvNNjx8OvTjPz9DBwRl6jT/SPwM=", // student123
                FirstName = "Вася",
                LastName = "Студент",
                Role = UserRole.Student,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            modelBuilder.Entity<User>().HasData(adminUser, admin2User, teacherUser, teacher2User, studentUser, student2User);
        }
    }