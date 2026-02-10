using Appointment_SaaS.Core.Entities;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tablolarımız
        public DbSet<Sector> Sectors { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. Senin Stilinde Primary Key Tanımlama (EntityIsmiID)
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var idPropertyName = $"{entityType.ClrType.Name}ID";
                if (entityType.FindProperty(idPropertyName) != null)
                {
                    modelBuilder.Entity(entityType.ClrType).HasKey(idPropertyName);
                }
            }

            // 2. İlişki Yapılandırmaları (Fluent API)
            modelBuilder.Entity<Tenant>()
                .HasOne(t => t.Sector)
                .WithMany(s => s.Tenants)
                .HasForeignKey(t => t.SectorID);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Tenant)
                .WithMany(t => t.Services)
                .HasForeignKey(s => s.TenantID);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Tenant)
                .WithMany(t => t.Appointments)
                .HasForeignKey(a => a.TenantID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.AppUser)
                .WithMany(u => u.Appointments)
                .HasForeignKey(a => a.AppUserID)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. SEED DATA (Başlangıç Verileri)

            // A. Sektörler
            modelBuilder.Entity<Sector>().HasData(
                new Sector { SectorID = 1, Name = "Erkek Kuaförü", DefaultPrompt = "Sen profesyonel bir erkek kuaförü asistanısın. Maskülen, net ve çözüm odaklı konuş." },
                new Sector { SectorID = 2, Name = "Kadın Kuaförü", DefaultPrompt = "Sen nazik ve detaycı bir kadın kuaförü asistanısın. Estetik ve bakım konularına hakim konuş." },
                new Sector { SectorID = 3, Name = "Unisex Kuaför", DefaultPrompt = "Sen modern ve kapsayıcı bir kuaför asistanısın. Her türlü bakım hizmetine uygun profesyonel bir dille konuş." }
            );

            modelBuilder.Entity<Tenant>().HasData(
     new Tenant
     {
         TenantID = 1,
         Name = "Janti Erkek Kuaförü",
         Address = "İstanbul, Şişli No:10",
         ApiKey = "JNT-123-ABC",
         WabaID = "W101",
         PhoneNumber = "5551112233",
         SectorID = 1, // Erkek Kuaförü
         CreatedAt = DateTime.Now,
         IsActive = true
     },
     new Tenant
     {
         TenantID = 2,
         Name = "Işıltı Bayan Salonu",
         Address = "Ankara, Çankaya No:25",
         ApiKey = "ISL-456-DEF",
         WabaID = "W202",
         PhoneNumber = "5552223344",
         SectorID = 2, // Bayan Kuaförü
         CreatedAt = DateTime.Now,
         IsActive = true
     },
     new Tenant
     {
         TenantID = 3,
         Name = "Modern Tarz Unisex",
         Address = "İzmir, Alsancak No:5",
         ApiKey = "MOD-789-GHI",
         WabaID = "W303",
         PhoneNumber = "5553334455",
         SectorID = 3, // Unisex
         CreatedAt = DateTime.Now,
         IsActive = true
     }
 
           
            );

            // C. Hizmetler (Services)
            modelBuilder.Entity<Service>().HasData(
            // Janti Erkek Kuaförü (ID 1-3)
                new Service { ServiceID = 1, TenantID = 1, Name = "Saç Kesimi", Price = 250, DurationInMinutes = 30 },
            new Service { ServiceID = 2, TenantID = 1, Name = "Sakal Tıraşı", Price = 150, DurationInMinutes = 20 },
            new Service { ServiceID = 3, TenantID = 1, Name = "Cilt Bakımı", Price = 400, DurationInMinutes = 45 },

            // Işıltı Bayan Salonu (ID 4-6)
                new Service { ServiceID = 4, TenantID = 2, Name = "Fön", Price = 200, DurationInMinutes = 30 },
            new Service { ServiceID = 5, TenantID = 2, Name = "Boya", Price = 1200, DurationInMinutes = 120 },
                new Service { ServiceID = 6, TenantID = 2, Name = "Manikür", Price = 350, DurationInMinutes = 40 },

                // Modern Tarz Unisex (ID 7-9)
                new Service { ServiceID = 7, TenantID = 3, Name = "Modern Kesim", Price = 500, DurationInMinutes = 60 },
                new Service { ServiceID = 8, TenantID = 3, Name = "Keratin Bakım", Price = 1500, DurationInMinutes = 90 },
                new Service { ServiceID = 9, TenantID = 3, Name = "Kaş Dizayn", Price = 300, DurationInMinutes = 30 }
            );

            base.OnModelCreating(modelBuilder);
        }

    }
}
    

