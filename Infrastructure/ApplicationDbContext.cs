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
            var seedDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
    
            // Группы
            var groups = new[]
            {
                new Group 
                { 
                    Id = Guid.Parse("3a6b8c9d-4e5f-6a7b-8c9d-0e1f2a3b4c5d"),
                    Name = "ИТ-21",
                    Description = "Группа информационных технологий 2021",
                    CreatedAt = seedDate,
                    UpdatedAt = null
                },
                new Group 
                { 
                    Id = Guid.Parse("5d4c3b2a-1f0e-9d8c-7b6a-5f4e3d2c1b0a"),
                    Name = "БУ-21", 
                    Description = "Группа бухгалтеров 2021",
                    CreatedAt = seedDate,
                    UpdatedAt = null
                },
                new Group 
                { 
                    Id = Guid.Parse("9e8f7a6b-5c4d-3e2f-1a0b-9c8d7e6f5a4b"),
                    Name = "ИТ-22",
                    Description = "Группа информационных технологий 2022",
                    CreatedAt = seedDate,
                    UpdatedAt = null
                },
                new Group 
                { 
                    Id = Guid.Parse("b0a1c2d3-e4f5-6a7b-8c9d-e0f1a2b3c4d5"),
                    Name = "Исип22/1",
                    Description = "Группа информационных технологий 2022",
                    CreatedAt = seedDate,
                    UpdatedAt = null
                }
            };

            modelBuilder.Entity<Group>().HasData(groups);

            // Пользовател
            var adminUser = new User
            {
                Id = Guid.Parse("c8d7e6f5-a4b3-c2d1-e0f9-8a7b6c5d4e3f"),
                Email = "admin@scfet.ru",
                PasswordHash = "10000.KtO5UPCwLgoZiPkTPcBFdg==.GnqlYRsAlmYbaoEHbrgC7ISjPuArcsKmYHgmJ0fPTy8=",
                FirstName = "Администратор",
                LastName = "Системы",
                Role = UserRole.Administrator,
                CreatedAt = seedDate,
                IsActive = true,
                RefreshToken = null,
                RefreshTokenExpiryTime = null,
                GroupId = null,
                DeviceToken = null,
                ChatId = null,
                UpdatedAt = null
            };
            
            var admin2User = new User
            {
                Id = Guid.Parse("f4e5d6c7-b8a9-0f1e-2d3c-4b5a6f7e8d9c"),
                Email = "admin2@scfet.ru",
                PasswordHash = "10000.KtO5UPCwLgoZiPkTPcBFdg==.GnqlYRsAlmYbaoEHbrgC7ISjPuArcsKmYHgmJ0fPTy8=",
                FirstName = "Администратор2",
                LastName = "Системы",
                Role = UserRole.Administrator,
                CreatedAt = seedDate,
                IsActive = true,
                RefreshToken = null,
                RefreshTokenExpiryTime = null,
                GroupId = null,
                DeviceToken = null,
                ChatId = null,
                UpdatedAt = null
            };

            var teacherUser = new User
            {
                Id = Guid.Parse("2a3b4c5d-6e7f-89a0-b1c2-d3e4f5a6b7c8"),
                Email = "teacher@scfet.ru",
                PasswordHash = "10000.SR7TWPsSkx86MW5kqhsk9g==.fmFOgSZurlhe/T6sBgfgE7V9liHsKaCdybPEyXCXREA=",
                FirstName = "Иван",
                LastName = "Преподаватель",
                Role = UserRole.Teacher,
                CreatedAt = seedDate,
                IsActive = true,
                RefreshToken = null,
                RefreshTokenExpiryTime = null,
                GroupId = null,
                DeviceToken = null,
                ChatId = null,
                UpdatedAt = null
            };
            
            var teacher2User = new User
            {
                Id = Guid.Parse("9b8c7d6e-5f4a-3b2c-1d0e-f9a8b7c6d5e4"),
                Email = "teacher2@scfet.ru",
                PasswordHash = "10000.SR7TWPsSkx86MW5kqhsk9g==.fmFOgSZurlhe/T6sBgfgE7V9liHsKaCdybPEyXCXREA=",
                FirstName = "Алексей",
                LastName = "Преподаватель",
                Role = UserRole.Teacher,
                CreatedAt = seedDate,
                IsActive = true,
                RefreshToken = null,
                RefreshTokenExpiryTime = null,
                GroupId = null,
                DeviceToken = null,
                ChatId = null,
                UpdatedAt = null
            };
            
            var studentUser = new User
            {
                Id = Guid.Parse("7e6d5c4b-3a29-1f0e-dcba-9876f5e4d3c2"),
                Email = "student@scfet.ru",
                PasswordHash = "10000.ubXk5yxtgQgWcc3/M+w4eQ==.U2b8/891ganlfSu1GvNNjx8OvTjPz9DBwRl6jT/SPwM=",
                FirstName = "Михаил",
                LastName = "Студент",
                Role = UserRole.Student,
                CreatedAt = seedDate,
                IsActive = true,
                RefreshToken = null,
                RefreshTokenExpiryTime = null,
                GroupId = null,
                DeviceToken = null,
                ChatId = null,
                UpdatedAt = null
            };
            
            var student2User = new User
            {
                Id = Guid.Parse("d3c2b1a0-9f8e-7d6c-5b4a-392817f6e5d4"),
                Email = "student2@scfet.ru",
                PasswordHash = "10000.ubXk5yxtgQgWcc3/M+w4eQ==.U2b8/891ganlfSu1GvNNjx8OvTjPz9DBwRl6jT/SPwM=",
                FirstName = "Вася",
                LastName = "Студент",
                Role = UserRole.Student,
                CreatedAt = seedDate,
                IsActive = true,
                RefreshToken = null,
                RefreshTokenExpiryTime = null,
                GroupId = null,
                DeviceToken = null,
                ChatId = null,
                UpdatedAt = null
            };
            
            modelBuilder.Entity<User>().HasData(
                adminUser, admin2User, teacherUser, teacher2User, studentUser, student2User
            );
        }
    }