using System.Threading.Channels;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Fire-and-forget queue for outgoing emails. Slices call <see cref="Queue"/>
/// from request handlers without awaiting the actual send: the message lands
/// in an unbounded in-memory channel and a hosted service drains it on a
/// background scope. Pending messages are lost on restart — that's the cost
/// of skipping persistence and is acceptable for non-critical notifications.
/// </summary>
public sealed class EmailDispatchService
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>Reader consumed by the background drainer. Internal coupling — not for slice use.</summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    /// <summary>Enqueue a message. Returns immediately; never throws.</summary>
    /// <param name="message">Composed email payload.</param>
    public void Queue(EmailMessage message)
    {
        // Channel is unbounded so TryWrite never fails — the bool is ignored.
        _channel.Writer.TryWrite(message);
    }
}
