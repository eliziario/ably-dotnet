using System.Threading.Tasks;
using FluentAssertions;
using IO.Ably.Realtime;
using IO.Ably.Transport.States.Connection;
using IO.Ably.Types;
using Xunit;
using Xunit.Abstractions;

namespace IO.Ably.Tests
{
    public class ClosedStateSpecs : AblySpecs
    {
        private FakeConnectionContext _context;
        private ConnectionClosedState _state;

        public ClosedStateSpecs(ITestOutputHelper output) : base(output)
        {
            _context = new FakeConnectionContext();
            _state = new ConnectionClosedState(_context);
        }

        [Fact]
        public void ShouldHaveCorrectState()
        {
            _state.State.Should().Be(ConnectionStateType.Closed);
        }

        [Fact]
        public void WhenConnectCalled_MovesToConnectingState()
        {
            // Act
            _state.Connect();

            // Assert
            _context.StateShouldBe<ConnectionConnectingState>();
        }

        [Fact]
        public void WhenCloseCalled_ShouldDoNothing()
        {
            // Act
            new ConnectionClosedState(null).Close();
        }

        [Fact]
        public void WhenMessageIsSent_DoesNothing()
        {
            // Act
            _state.SendMessage(new ProtocolMessage(ProtocolMessage.MessageAction.Attach));
        }

        [Fact]
        public async Task OnAttachedToContext_ShouldDestroyTransport()
        {
            // Arrange
            _context.Transport = new FakeTransport();

            // Act
            await _state.OnAttachedToContext();

            // Assert
            _context.DestroyTransportCalled.Should().BeTrue();
        }

        [Theory]
        [InlineData(ProtocolMessage.MessageAction.Ack)]
        [InlineData(ProtocolMessage.MessageAction.Attach)]
        [InlineData(ProtocolMessage.MessageAction.Attached)]
        [InlineData(ProtocolMessage.MessageAction.Close)]
        [InlineData(ProtocolMessage.MessageAction.Closed)]
        [InlineData(ProtocolMessage.MessageAction.Connect)]
        [InlineData(ProtocolMessage.MessageAction.Connected)]
        [InlineData(ProtocolMessage.MessageAction.Detach)]
        [InlineData(ProtocolMessage.MessageAction.Detached)]
        [InlineData(ProtocolMessage.MessageAction.Disconnect)]
        [InlineData(ProtocolMessage.MessageAction.Disconnected)]
        [InlineData(ProtocolMessage.MessageAction.Error)]
        [InlineData(ProtocolMessage.MessageAction.Heartbeat)]
        [InlineData(ProtocolMessage.MessageAction.Message)]
        [InlineData(ProtocolMessage.MessageAction.Nack)]
        [InlineData(ProtocolMessage.MessageAction.Presence)]
        [InlineData(ProtocolMessage.MessageAction.Sync)]
        public async Task ShouldNotHandleInboundMessageWithAction(ProtocolMessage.MessageAction action)
        {
            // Act
            bool result = await _state.OnMessageReceived(new ProtocolMessage(action));

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public async Task ShouldClearConnectionKey()
        {
            // Arrange
            _context.Connection.Key = "test";

            // Act
            await _state.OnAttachedToContext();

            // Assert
            _context.Connection.Key.Should().BeNullOrEmpty();
        }
    }
}