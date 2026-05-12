using FamilyNido.Domain.Agenda;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="MemberAgendaException"/>.</summary>
public sealed class MemberAgendaExceptionConfiguration : IEntityTypeConfiguration<MemberAgendaException>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemberAgendaException> builder)
    {
        builder.ToTable("member_agenda_exceptions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TransportMode).HasConversion<int?>();

        builder.Property(e => e.Label).HasMaxLength(120);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.UpdatedBy).HasMaxLength(200);

        builder.HasOne(e => e.FamilyMember)
            .WithMany()
            .HasForeignKey(e => e.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // When a pattern is deleted, drop its overrides too — they no longer
        // refer to anything. Ad-hoc rows (PatternId is null) are unaffected.
        builder.HasOne(e => e.Pattern)
            .WithMany()
            .HasForeignKey(e => e.PatternId)
            .OnDelete(DeleteBehavior.Cascade);

        // PatternId is nullable: PG treats nulls as distinct, so multiple
        // ad-hoc rows on the same (member, date) coexist while pattern
        // overrides stay unique per (member, date, pattern).
        builder.HasIndex(e => new { e.FamilyMemberId, e.Date, e.PatternId }).IsUnique();
        builder.HasIndex(e => new { e.FamilyId, e.Date });
    }
}
