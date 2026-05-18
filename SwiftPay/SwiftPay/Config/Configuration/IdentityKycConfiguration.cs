using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwiftPay.Constants.Enums;
using SwiftPay.Domain.Remittance.Entities;
using SwiftPay.Models;

namespace SwiftPay.Config.Configuration
{
    // User Configuration for Identity and KYC
    public class UserIdentityKycConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.UserId);
            builder.Property(u => u.UserId).ValueGeneratedOnAdd();

            builder.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(255);

            // Email with filtered UNIQUE constraint (ignore soft-deleted rows)
            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);
            builder.HasIndex(u => u.Email)
                   .HasFilter("[IsDeleted] = 0")
                   .IsUnique();

            // Phone with filtered UNIQUE constraint
            builder.Property(u => u.Phone)
                .HasMaxLength(10);
            builder.HasIndex(u => u.Phone)
                   .HasFilter("[IsDeleted] = 0")
                   .IsUnique();

            // PasswordHash (only the hash is stored)
            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(128);

            builder.Property(u => u.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(UserStatus.Active);

            // IsDeleted with default value false
            builder.Property(u => u.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Global query filter - hide soft-deleted users by default
            builder.HasQueryFilter(u => !u.IsDeleted);

            // CreatedAt with default current timestamp
            builder.Property(u => u.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // UpdatedAt with default current timestamp
            builder.Property(u => u.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation (restrict deletes to preserve history)
            builder.HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    // Role Configuration
    public class RoleIdentityKycConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(r => r.RoleId);
            builder.Property(r => r.RoleId).ValueGeneratedOnAdd();

            builder.Property(r => r.RoleType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(r => r.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(r => r.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation
            builder.HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(r => r.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Global query filter to hide soft-deleted roles
            builder.HasQueryFilter(r => !r.IsDeleted);
        }
    }

    // UserRole Configuration
    public class UserRoleIdentityKycConfiguration : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.HasKey(ur => ur.UserRoleId);
            builder.Property(ur => ur.UserRoleId).ValueGeneratedOnAdd();

            builder.Property(ur => ur.UserId).IsRequired();
            builder.Property(ur => ur.RoleId).IsRequired();

            builder.Property(ur => ur.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(ur => ur.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(ur => ur.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Foreign keys - restrict deletes
            builder.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(ur => ur.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Ensure (UserId, RoleId) is unique while ignoring soft-deleted rows
            builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
                   .HasFilter("[IsDeleted] = 0")
                   .IsUnique();

            // Global query filter to hide soft-deleted user-role assignments
            builder.HasQueryFilter(ur => !ur.IsDeleted);
        }
    }

    // KYC Document Configuration
    public class KYCDocumentConfiguration : IEntityTypeConfiguration<SwiftPay.Models.KYCDocument>
    {
        public void Configure(EntityTypeBuilder<SwiftPay.Models.KYCDocument> builder)
        {
            builder.HasKey(d => d.KYCDocumentId);
            builder.Property(d => d.KYCDocumentId).ValueGeneratedOnAdd();

            builder.Property(d => d.KYCID).IsRequired();

            builder.Property(d => d.DocType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(d => d.FileURI)
                .IsRequired()
                .HasMaxLength(2048);

            builder.Property(d => d.UploadedDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(d => d.VerificationStatus)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(KycVerificationStatus.Pending);

            builder.Property(d => d.Notes).HasColumnType("text").IsRequired(false);

            builder.Property(d => d.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(d => d.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);
            builder.HasQueryFilter(d => !d.IsDeleted);

            builder.HasOne(d => d.KYC)
                .WithMany()
                .HasForeignKey(d => d.KYCID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // KYC Record Configuration
    public class KYCRecordConfiguration : IEntityTypeConfiguration<KYCRecord>
    {
        public void Configure(EntityTypeBuilder<KYCRecord> builder)
        {
            builder.HasKey(k => k.KYCID);
            builder.Property(k => k.KYCID).ValueGeneratedOnAdd();

            builder.Property(k => k.UserID)
                .IsRequired();

            // Map KYCLevel enum to VARCHAR
            builder.Property(k => k.KYCLevel)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            // Map VerificationStatus enum to VARCHAR with default Pending (enum value)
            builder.Property(k => k.VerificationStatus)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(KycVerificationStatus.Pending);

            builder.Property(k => k.VerifiedDate)
                .IsRequired(false);

            builder.Property(k => k.Notes)
                .HasColumnType("text")
                .IsRequired(false);

            // CreatedAt with default current timestamp
            builder.Property(k => k.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // UpdatedAt with default current timestamp
            builder.Property(k => k.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // IsDeleted with default value false
            builder.Property(k => k.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Foreign key to User - Restrict delete to preserve KYC records
            builder.HasOne(k => k.User)
                .WithMany()
                .HasForeignKey(k => k.UserID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}