using ActualChat.Db.Module;
using ActualChat.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Db;

public abstract class DbInitializer<TDbContext> : DbServiceBase<TDbContext>, IDbInitializer
    where TDbContext : DbContext
{
    private bool? _shouldRecreateDb;

    protected DbInitializer(IServiceProvider services) : base(services) { }

    public bool ShouldRecreateDb {
        get => _shouldRecreateDb
            ?? (bool)(_shouldRecreateDb = Services.GetRequiredService<DbInfo<TDbContext>>().ShouldRecreateDb);
        set => _shouldRecreateDb = value;
    }

    public Dictionary<IDbInitializer, Task> InitializeTasks { get; set; } = null!;

    public virtual async Task Initialize(CancellationToken cancellationToken)
    {
        await using var dbContext = DbContextFactory.CreateDbContext();
        var db = dbContext.Database;
        if (ShouldRecreateDb)
            await db.EnsureDeletedAsync(cancellationToken);
        await db.EnsureCreatedAsync(cancellationToken);
    }
}
