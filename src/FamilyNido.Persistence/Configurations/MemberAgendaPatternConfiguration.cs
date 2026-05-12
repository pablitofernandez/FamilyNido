using FamilyNido.Domain.Agenda;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="MemberAgendaPattern"/>.</summary>
public sealed class MemberAgendaPatternConfiguration : IEntityTypeConfiguration<MemberAgendaPattern>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemberAgendaPattern> builder)
    {
        builder.ToTable("member_agenda_patterns");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DayOfWeek).HasConversion<int>();
        builder.Property(p => p.TransportMode).HasConversion<int>();

        builder.Property(p => p.Label).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Location).HasMaxLength(200);
        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(200);
        builder.Property(p => p.UpdatedBy).HasMaxLength(200);

        builder.HasOne(p => p.FamilyMember)
            .WithMany()
            .HasForeignKey(p => p.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.FamilyMemberId, p.DayOfWeek });
        builder.HasIndex(p => p.FamilyId);
    }
}
