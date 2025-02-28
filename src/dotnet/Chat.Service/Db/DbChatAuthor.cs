using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActualChat.Chat.Db;

[Table("ChatAuthors")]
[Index(nameof(ChatId), nameof(LocalId))]
[Index(nameof(ChatId), nameof(UserId))]
public class DbChatAuthor : IHasId<string>
{
    [Key] public string Id { get; set; } = null!;
    string IHasId<string>.Id => Id;

    public string ChatId { get; set; } = null!;
    public long LocalId { get; set; }

    [ConcurrencyCheck] public long Version { get; set; }
    public string Name { get; set; } = "";
    public bool IsAnonymous { get; set; }
    public string? UserId { get; set; }

    public static string ComposeId(string chatId, long localId)
        => $"{chatId}:{localId.ToString(CultureInfo.InvariantCulture)}";

    public ChatAuthor ToModel()
        => new() {
            Id = Id,
            ChatId = ChatId,
            Version = Version,
            Name = Name,
            IsAnonymous = IsAnonymous,
            UserId = UserId ?? "",
        };

    internal class EntityConfiguration : IEntityTypeConfiguration<DbChatAuthor>
    {
        public void Configure(EntityTypeBuilder<DbChatAuthor> builder)
        {
            builder.Property(a => a.Id).IsRequired();
            builder.Property(a => a.ChatId).IsRequired();
        }
    }
}
