using System.Text.Json;

using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurchProjection.Infrastructure.Persistence.Configurations;

public sealed class ServicePlanConfiguration : IEntityTypeConfiguration<ServicePlan>
{
    public void Configure(EntityTypeBuilder<ServicePlan> builder)
    {
        builder.ToTable("services");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasConversion(id => id.Value, value => new ServiceId(value));
        builder.Property(s => s.Name).HasColumnName("name").IsRequired();
        builder.Property(s => s.ServiceDate).HasColumnName("service_date");

        // The aggregate exposes IReadOnlyList and owns renumbering, so EF reads
        // and writes the backing field rather than going through the property.
        builder.Metadata
            .FindNavigation(nameof(ServicePlan.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(s => s.Items, items =>
        {
            items.ToTable("service_items");
            items.WithOwner().HasForeignKey("service_id");
            items.HasKey(i => i.Id);
            // ServiceItem.Id is a plain string; ItemId is the live aggregate's
            // wrapper over the same value and never reaches the database.
            items.Property(i => i.Id).HasColumnName("id");
            items.Property(i => i.Kind).HasColumnName("kind").IsRequired();
            items.Property(i => i.Label).HasColumnName("label").IsRequired();
            items.Property(i => i.Position).HasColumnName("position");
            items.Property(i => i.Ref)
                .HasColumnName("ref_json")
                .IsRequired()
                .HasConversion(
                    reference => JsonSerializer.Serialize(reference, ItemRefJson.Options),
                    json => JsonSerializer.Deserialize<ItemRef>(json, ItemRefJson.Options)!,
                    new ValueComparer<ItemRef>(
                        (left, right) => left == right,
                        reference => reference.GetHashCode(),
                        // Identity snapshot: ItemRef is init-only all the way
                        // down, so there is nothing to deep-copy. A `with`
                        // expression is illegal inside an expression tree.
                        reference => reference));
        });

        builder.Navigation(s => s.Items).AutoInclude();
    }
}

internal static class ItemRefJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class SettingConfiguration : IEntityTypeConfiguration<SettingRow>
{
    public void Configure(EntityTypeBuilder<SettingRow> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasColumnName("key");
        builder.Property(s => s.Value).HasColumnName("value").IsRequired();
    }
}

public sealed class LiveStateConfiguration : IEntityTypeConfiguration<LiveStateRow>
{
    public void Configure(EntityTypeBuilder<LiveStateRow> builder)
    {
        builder.ToTable("live_state");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.ServiceId).HasColumnName("service_id");
        builder.Property(l => l.LiveItemId).HasColumnName("live_item_id");
        builder.Property(l => l.LivePageIndex).HasColumnName("live_page_index");
        builder.Property(l => l.LiveMediaAvailable).HasColumnName("live_media_available");
        builder.Property(l => l.PreviewItemId).HasColumnName("preview_item_id");
        builder.Property(l => l.PreviewPageIndex).HasColumnName("preview_page_index");
        builder.Property(l => l.PreviewMediaAvailable).HasColumnName("preview_media_available");
        builder.Property(l => l.Blackout).HasColumnName("blackout");
        builder.Property(l => l.SkippedJson).HasColumnName("skipped_json").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
    }
}
