using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KnowledgeHub.Practicums.Dtos;
using Volo.Abp;

namespace KnowledgeHub.Practicums;

/// <summary>
/// Singleton manager that holds SSE channels per project.
/// Each connected client gets a Channel<ChatMessageDto>. Broadcast writes to all channels.
/// </summary>
public class PracticumChatConnectionManager
{
    private const int MaxConnectionsPerProject = 20;
    private readonly ConcurrentDictionary<Guid, List<ChannelWriter<PracticumChatMessageDto>>> _rooms = new();

    public IDisposable Subscribe(Guid projectId, Channel<PracticumChatMessageDto> channel)
    {
        var list = _rooms.GetOrAdd(projectId, _ => new List<ChannelWriter<PracticumChatMessageDto>>());

        lock (list)
        {
            if (list.Count >= MaxConnectionsPerProject)
            {
                throw new UserFriendlyException("聊天室连接已满，请稍后再试。");
            }

            list.Add(channel.Writer);
        }

        return new Unsubscriber(() =>
        {
            lock (list)
            {
                list.Remove(channel.Writer);
                if (list.Count == 0)
                {
                    _rooms.TryRemove(projectId, out _);
                }
            }
        });
    }

    public async Task BroadcastAsync(Guid projectId, PracticumChatMessageDto message)
    {
        if (!_rooms.TryGetValue(projectId, out var channels))
        {
            return;
        }

        List<ChannelWriter<PracticumChatMessageDto>> snapshot;
        lock (channels)
        {
            snapshot = new List<ChannelWriter<PracticumChatMessageDto>>(channels);
        }

        foreach (var writer in snapshot)
        {
            try
            {
                if (!writer.TryWrite(message))
                {
                    await writer.WriteAsync(message);
                }
            }
            catch
            {
                // Channel closed or full — the subscriber cleanup will handle removal.
            }
        }
    }

    public int GetConnectionCount(Guid projectId)
    {
        if (!_rooms.TryGetValue(projectId, out var channels))
        {
            return 0;
        }

        lock (channels)
        {
            return channels.Count;
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _onDispose;

        public Unsubscriber(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}
