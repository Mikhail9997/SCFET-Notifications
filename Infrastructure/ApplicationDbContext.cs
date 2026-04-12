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
        public DbSet<NotificationReply> NotificationReplies => Set<NotificationReply>();
        public DbSet<NotificationReceiver> NotificationReceivers => Set<NotificationReceiver>();
        public DbSet<AvatarPreset> AvatarPresets => Set<AvatarPreset>();
        public DbSet<UserFavoriteNotification> UserFavoriteNotifications => Set<UserFavoriteNotification>();
        public DbSet<Channel> Channels => Set<Channel>();
        public DbSet<ChannelInvitation> ChannelInvitations => Set<ChannelInvitation>();
        public DbSet<ChannelUser> ChannelUsers => Set<ChannelUser>();
        public DbSet<ChannelMessage> ChannelMessages => Set<ChannelMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new GroupConfiguration());
            modelBuilder.ApplyConfiguration(new NotificationConfiguration());
            modelBuilder.ApplyConfiguration(new NotificationReceiverConfiguration());
            modelBuilder.ApplyConfiguration(new NotificationReplyConfiguration());
            modelBuilder.ApplyConfiguration(new AvatarPresetConfiguration());
            modelBuilder.ApplyConfiguration(new UserFavoriteNotificationConfiguration());
            modelBuilder.ApplyConfiguration(new ChannelConfiguration());
            modelBuilder.ApplyConfiguration(new ChannelInvitationConfiguration());
            modelBuilder.ApplyConfiguration(new ChannelUserConfiguration());
            modelBuilder.ApplyConfiguration(new ChannelMessageConfiguration());
        }
    }