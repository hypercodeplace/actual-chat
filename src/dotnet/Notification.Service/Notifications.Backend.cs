using ActualChat.Notification.Backend;
using ActualChat.Notification.Db;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Notification;

public partial class Notifications
{
    public virtual async Task<Device[]> GetDevices(string userId, CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        var dbDevices = await dbContext.Devices
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var entries = dbDevices.Select(d => d.ToModel()).ToArray();
        return entries;
    }

    public virtual async Task<string[]> GetSubscribers(string chatId, CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext();
        await using var _ = dbContext.ConfigureAwait(false);

        return await dbContext.ChatSubscriptions
            .Where(cs => cs.ChatId == chatId)
            .Select(cs => cs.UserId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task NotifySubscribers(
        INotificationsBackend.NotifySubscribersCommand notifyCommand,
        CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return;

        var (chatId, entryId, userId, title, content) = notifyCommand;
        var userIds = await GetSubscribers(chatId, cancellationToken).ConfigureAwait(false);
        var multicastMessage = new MulticastMessage {
            Tokens = null,
            Notification = new FirebaseAdmin.Messaging.Notification {
                Title = title,
                Body = content,
                // ImageUrl = ??? TODO(AK): set image url
            },
            Android = new AndroidConfig {
                Notification = new AndroidNotification {
                    // Color = ??? TODO(AK): set color
                    Priority = NotificationPriority.DEFAULT,
                    // Sound = ??? TODO(AK): set sound
                    Tag = chatId,
                    Visibility = NotificationVisibility.PRIVATE,
                    // ClickAction = ?? TODO(AK): Set click action for Android
                    DefaultSound = true,
                    LocalOnly = false,
                    // NotificationCount = TODO(AK): Set unread message count!
                    // Icon = ??? TODO(AK): Set icon
                },
                Priority = Priority.Normal,
                CollapseKey = "topics",
                // RestrictedPackageName = TODO(AK): Set android package name
                TimeToLive = TimeSpan.FromMinutes(180),
            },
            Apns = new ApnsConfig {
                Aps = new Aps {
                    Sound = "default",
                    MutableContent = true,
                    ThreadId = "topics",
                },
            },
            Webpush = new WebpushConfig {
                Notification = new WebpushNotification {
                    Renotify = true,
                    Tag = chatId,
                    RequireInteraction = false,
                    // Icon = ??? TODO(AK): Set icon
                },
                FcmOptions = new WebpushFcmOptions {
                    // Link = ??? TODO(AK): Set anchor to open particular entry
                    Link = StringComparer.Ordinal.Equals(_uriMapper.BaseUri.Host, "localhost")
                        ? null
                        : _uriMapper.ToAbsolute($"/chat/{chatId}").ToString(),
                }
            },
        };
        var deviceIdGroups = userIds
            .Where(uid => !string.Equals(uid, userId, StringComparison.Ordinal))
            .ToAsyncEnumerable()
            .SelectMany(uid => GetDevicesInternal(uid, cancellationToken))
            .Chunk(200, cancellationToken);

        await foreach (var deviceGroup in deviceIdGroups.ConfigureAwait(false)) {
            multicastMessage.Tokens = deviceGroup.Select(p => p.DeviceId).ToList();
            var batchResponse = await _firebaseMessaging.SendMulticastAsync(multicastMessage, cancellationToken)
                .ConfigureAwait(false);

            if (batchResponse.FailureCount > 0)
                _log.LogWarning("Notification messages were not sent. NotificationCount = {NotificationCount}",
                    batchResponse.FailureCount);

            if (batchResponse.SuccessCount > 0)
                _ = Task.Run(
                    () => PersistMessages(chatId,
                        entryId,
                        deviceGroup,
                        batchResponse.Responses,
                        cancellationToken),
                    cancellationToken);
        }

        async IAsyncEnumerable<(string DeviceId, string UserId)> GetDevicesInternal(
            string userId1,
            [EnumeratorCancellation] CancellationToken cancellationToken1)
        {
            var devices = await GetDevices(userId1, cancellationToken1).ConfigureAwait(false);
            foreach (var device in devices)
                yield return (device.DeviceId, userId1);
        }

        async Task PersistMessages(
            string chatId1,
            long entryId1,
            IReadOnlyList<(string DeviceId, string UserId)> devices,
            IReadOnlyList<SendResponse> responses,
            CancellationToken cancellationToken1)
        {
            // TODO(AK): sharding by userId - code is running at a sharded service already
            var dbContext = _dbContextFactory.CreateDbContext().ReadWrite();
            await using var __ = dbContext.ConfigureAwait(false);

            var dbMessages = responses
                .Zip(devices)
                .Where(pair => pair.First.IsSuccess)
                .Select(pair => new DbMessage {
                    Id = pair.First.MessageId,
                    DeviceId = pair.Second.DeviceId,
                    ChatId = chatId1,
                    ChatEntryId = entryId1,
                    CreatedAt = _clocks.SystemClock.Now,
                });

            await dbContext.AddRangeAsync(dbMessages, cancellationToken1).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
