﻿using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace IO.Ably.Types
{
    public class ProtocolMessage
    {
        private string _connectionKey;

        public enum MessageAction : int
        {
            Heartbeat = 0,
            Ack,
            Nack,
            Connect,
            Connected,
            Disconnect,
            Disconnected,
            Close,
            Closed,
            Error,
            Attach,
            Attached,
            Detach,
            Detached,
            Presence,
            Message,
            Sync
        }

        [Flags]
        public enum MessageFlag : byte
        {
            Has_Presence,
            Has_Backlog
        }

        public ProtocolMessage()
        {
        }

        internal ProtocolMessage(MessageAction action)
        {
            this.action = action;
        }

        internal ProtocolMessage(MessageAction action, string channel)
        {
            this.action = action;
            this.channel = channel;
        }

        public MessageAction action { get; set; }

        public MessageFlag? flags { get; set; }

        public int? count { get; set; }

        public ErrorInfo error { get; set; }
        public string id { get; set; }
        public string channel { get; set; }
        public string channelSerial { get; set; }
        public string connectionId { get; set; }

        public string connectionKey
        {
            get
            {
                if(connectionDetails != null && connectionDetails.connectionKey.IsNotEmpty())
                    return connectionDetails.connectionKey;
                return _connectionKey;
            }
            set { _connectionKey = value; }
        }

        public long? connectionSerial { get; set; }
        public long msgSerial { get; set; }
        public DateTimeOffset? timestamp { get; set; }
        public Message[] messages { get; set; }

        public PresenceMessage[] presence { get; set; }
        public ConnectionDetailsMessage connectionDetails { get; set; }

        public bool ShouldSerializemessages()
        {
            if (null == messages)
                return false;
            return messages.Any(m => !m.IsEmpty);
        }

        [OnSerializing]
        internal void onSerializing(StreamingContext context)
        {
            if (channel == "")
                channel = null;

            // Filter out empty messages
            if (messages == null)
                return;

            messages = messages.Where(m => !m.IsEmpty).ToArray();
            if (messages.Length <= 0)
                messages = null;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append("{ ")
                .AppendFormat("action={0}", action);
            if (messages != null && messages.Length > 0)
            {
                text.Append(", mesasages=[ ");
                foreach (var message in messages)
                {
                    text.AppendFormat("{{ name=\"{0}\"", message.name);
                    if (message.timestamp.HasValue && message.timestamp.Value.Ticks > 0)
                    {
                        text.AppendFormat(", timestamp=\"{0}\"}}", message.timestamp);
                    }
                    text.AppendFormat(", data={0}}}", message.data);
                }
                text.Append(" ]");
            }
            text.Append(" }");
            return text.ToString();
        }
    }
}