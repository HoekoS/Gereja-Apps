using ChurchProjection.Domain.Library;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurchProjection.Infrastructure.Persistence.Configurations;

public sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable("translations");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").HasConversion(id => id.Value, value => new TranslationId(value));
        builder.Property(t => t.Abbrev).HasColumnName("abbrev").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.Language).HasColumnName("language").IsRequired();
    }
}

public sealed class BookNameConfiguration : IEntityTypeConfiguration<BookNameRow>
{
    public void Configure(EntityTypeBuilder<BookNameRow> builder)
    {
        builder.ToTable("book_names");
        builder.HasKey(b => new { b.TranslationId, b.BookId });
        builder.Property(b => b.TranslationId).HasColumnName("translation_id");
        builder.Property(b => b.BookId).HasColumnName("book_id");
        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Abbrev).HasColumnName("abbrev");
    }
}

public sealed class VerseConfiguration : IEntityTypeConfiguration<Verse>
{
    public void Configure(EntityTypeBuilder<Verse> builder)
    {
        builder.ToTable("verses");

        // An explicit rowid, because verses_fts is an external-content FTS5
        // table keyed on it.
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(v => v.TranslationId).HasColumnName("translation_id")
            .HasConversion(id => id.Value, value => new TranslationId(value));
        builder.Property(v => v.BookId).HasColumnName("book_id");
        builder.Property(v => v.Chapter).HasColumnName("chapter");
        builder.Property(v => v.Number).HasColumnName("verse");
        builder.Property(v => v.Text).HasColumnName("text").IsRequired();

        builder.HasIndex(v => new { v.TranslationId, v.BookId, v.Chapter, v.Number }).IsUnique();
    }
}

public sealed class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.ToTable("songs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasConversion(id => id.Value, value => new SongId(value));
        builder.Property(s => s.Title).HasColumnName("title").IsRequired();
        builder.Property(s => s.Author).HasColumnName("author");
        builder.Property(s => s.Ccli).HasColumnName("ccli");
        builder.Property(s => s.Language).HasColumnName("language");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsMany(s => s.Pages, pages =>
        {
            pages.ToTable("song_pages");
            pages.WithOwner().HasForeignKey("song_id");
            pages.HasKey("song_id", nameof(SongPage.Position));
            // Position is part of the key and is set by the aggregate, not by
            // SQLite. Without this EF treats an integer key as store-generated
            // and leaves the column out of the INSERT.
            pages.Property(p => p.Position).HasColumnName("position").ValueGeneratedNever();
            pages.Property(p => p.SectionLabel).HasColumnName("section_label");
            pages.Property(p => p.Text).HasColumnName("text").IsRequired();
        });

        builder.Navigation(s => s.Pages).AutoInclude();
    }
}

public sealed class MediaConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").HasConversion(id => id.Value, value => new MediaId(value));
        builder.Property(m => m.Kind).HasColumnName("kind").IsRequired();
        builder.Property(m => m.Filename).HasColumnName("filename").IsRequired();
        builder.Property(m => m.DurationMs).HasColumnName("duration_ms");
        builder.Property(m => m.Width).HasColumnName("width");
        builder.Property(m => m.Height).HasColumnName("height");
    }
}
