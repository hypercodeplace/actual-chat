using System.ComponentModel.DataAnnotations.Schema;
using Stl.Fusion.EntityFramework.Authentication;

namespace ActualChat.Users.Db;

public class DbUser : DbUser<string>
{
    private DateTime _createdAt = CoarseSystemClock.Now;

    public DateTime CreatedAt {
        get => _createdAt.DefaultKind(DateTimeKind.Utc);
        set => _createdAt = value.DefaultKind(DateTimeKind.Utc);
    }

    [Column(TypeName = "smallint")]
    public UserStatus Status { get; set; }
}
