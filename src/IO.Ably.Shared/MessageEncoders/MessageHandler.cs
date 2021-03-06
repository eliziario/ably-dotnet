using MsgPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using IO.Ably.Realtime;
using IO.Ably.Types;

namespace IO.Ably.MessageEncoders
{
    internal class MessageHandler
    {
        private static readonly Type[] UnsupportedTypes = new[]
            {
                typeof(short), typeof(int), typeof(double), typeof(float), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(byte), typeof(bool),
                typeof(long), typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
            };
        private readonly Protocol _protocol;
        public readonly List<MessageEncoder> Encoders = new List<MessageEncoder>();

        public MessageHandler()
            : this(Protocol.MsgPack)
        {

        }

        public MessageHandler(Protocol protocol)
        {
            _protocol = protocol;

            InitializeMessageEncoders(protocol);
        }

        private void InitializeMessageEncoders(Protocol protocol)
        {
            Encoders.Add(new JsonEncoder(protocol));
            Encoders.Add(new Utf8Encoder(protocol));
            Encoders.Add(new CipherEncoder(protocol));
            Encoders.Add(new Base64Encoder(protocol));

            Logger.Debug(string.Format("Initializing message encodings. {0} initialized", string.Join(",", Encoders.Select(x => x.EncodingName))));
        }

        public IEnumerable<PresenceMessage> ParsePresenceMessages(AblyResponse response, ChannelOptions options)
        {
            if (response.Type == ResponseType.Json)
            {
                var messages = JsonHelper.Deserialize<List<PresenceMessage>>(response.TextResponse);
                ProcessMessages(messages, options);
                return messages;
            }

            var payloads = MsgPackHelper.Deserialise(response.Body, typeof(List<PresenceMessage>)) as List<PresenceMessage>;
            ProcessMessages(payloads, options);
            return payloads;
        }

        public IEnumerable<Message> ParseMessagesResponse(AblyResponse response, ChannelOptions options)
        {
            Contract.Assert(options != null);

            if (response.Type == ResponseType.Json)
            {
                var messages = JsonHelper.Deserialize<List<Message>>(response.TextResponse);
                ProcessMessages(messages, options);
                return messages;
            }

            var payloads = MsgPackHelper.Deserialise(response.Body, typeof(List<Message>)) as List<Message>;
            ProcessMessages(payloads, options);
            return payloads;
        }

        private void ProcessMessages<T>(IEnumerable<T> payloads, ChannelOptions options) where T : IMessage
        {
            DecodePayloads(options, payloads as IEnumerable<IMessage>);
        }

        public void SetRequestBody(AblyRequest request)
        {
            request.RequestBody = GetRequestBody(request);
            if (_protocol == Protocol.MsgPack && Logger.IsDebug)
            {
                LogRequestBody(request.RequestBody);
            }
        }

        private void LogRequestBody(byte[] requestBody)
        {
            try
            {
                var body = MsgPackHelper.Deserialise(requestBody, typeof(MessagePackObject))?.ToString();
                
                Logger.Debug("RequestBody: " + (body ?? "No body present"));
            }
            catch (Exception ex)
            {
                Logger.Error("Error while logging request body.", ex);
            }
        }

        public byte[] GetRequestBody(AblyRequest request)
        {
            if (request.PostData == null)
                return new byte[] { };

            if (request.PostData is IEnumerable<Message>)
                return GetMessagesRequestBody(request.PostData as IEnumerable<Message>,
                    request.ChannelOptions);

            byte[] result;
            if (_protocol == Protocol.Json)
                result = JsonHelper.Serialize(request.PostData).GetBytes();
            else
            {
                result = MsgPackHelper.Serialise(request.PostData);
            }
            if (Logger.IsDebug) Logger.Debug("Request body: " + result.GetText());

            return result;
        }

        private byte[] GetMessagesRequestBody(IEnumerable<Message> payloads, ChannelOptions options)
        {
            EncodePayloads(options, payloads);

            if (_protocol == Protocol.MsgPack)
            {
                return MsgPackHelper.Serialise(payloads);
            }
            return JsonHelper.Serialize(payloads).GetBytes();
        }

        internal Result EncodePayloads(ChannelOptions options, IEnumerable<IMessage> payloads)
        {
            var result = Result.Ok();
            foreach (var payload in payloads)
                result = Result.Combine(result, EncodePayload(payload, options));

            return result;
        }

        internal Result DecodePayloads(ChannelOptions options, IEnumerable<IMessage> payloads)
        {
            var result = Result.Ok();
            foreach (var payload in payloads)
                result = Result.Combine(result, DecodePayload(payload, options));

            return result;
        }

        private Result EncodePayload(IMessage payload, ChannelOptions options)
        {
            ValidatePayloadDataType(payload);
            var result = Result.Ok();
            foreach (var encoder in Encoders)
            {
                result = Result.Combine(result, encoder.Encode(payload, options));
            }
            return result;
        }

        private void ValidatePayloadDataType(IMessage payload)
        {
            if (payload.Data == null)
                return;

            var dataType = payload.Data.GetType();
            var testType = GetNullableType(dataType) ?? dataType;
            if (UnsupportedTypes.Contains(testType))
            {
                throw new AblyException("Unsupported payload type. Only string, binarydata (byte[]) and objects convertable to json are supported being directly sent. This ensures that libraries in different languages work correctly. To send the requested value please create a DTO and pass the DTO as payload. For example if you are sending an '10' then create a class with one property; assign the value to the property and send it.");
            }
        }

        static Type GetNullableType(Type type)
        {
            if (type.IsValueType == false) return null; // ref-type
            return Nullable.GetUnderlyingType(type);
        }

        private Result DecodePayload(IMessage payload, ChannelOptions options)
        {
            var result = Result.Ok();
            foreach (var encoder in (Encoders as IEnumerable<MessageEncoder>).Reverse())
            {
                result = Result.Combine(result, encoder.Decode(payload, options));
            }

            return result;
        }

        /// <summary>Parse paginated response using specified parser function.</summary>
        /// <typeparam name="T">Item type</typeparam>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="funcParse">Function to parse HTTP response into a sequence of items.</param>
        /// <returns></returns>
        internal static PaginatedResult<T> Paginated<T>(AblyRequest request, AblyResponse response, Func<HistoryRequestParams, Task<PaginatedResult<T>>> executeDataQueryRequest) where T : class
        {
            PaginatedResult<T> res = new PaginatedResult<T>(response.Headers, GetLimit(request), executeDataQueryRequest);
            return res;
        }

        public PaginatedResult<T> ParsePaginatedResponse<T>(AblyRequest request, AblyResponse response, Func<HistoryRequestParams, Task<PaginatedResult<T>>> executeDataQueryRequest) where T : class
        {
            LogResponse(response);
            var result = Paginated(request, response, executeDataQueryRequest);
            var items = new List<T>();
            if (typeof(T) == typeof(Message))
            {
                var typedResult = result as PaginatedResult<Message>;
                typedResult.Items.AddRange(ParseMessagesResponse(response, request.ChannelOptions));
            }

            if (typeof(T) == typeof(Stats))
            {
                var typedResult = result as PaginatedResult<Stats>;
                typedResult?.Items.AddRange(ParseStatsResponse(response));
            }

            if (typeof(T) == typeof(PresenceMessage))
            {
                var typedResult = result as PaginatedResult<PresenceMessage>;
                typedResult.Items.AddRange(ParsePresenceMessages(response, request.ChannelOptions));
            }

            return result;
        }

        public T ParseResponse<T>(AblyRequest request, AblyResponse response) where T : class
        {
            LogResponse(response);

            var responseText = response.TextResponse;
            if (_protocol == Protocol.MsgPack)
            {
                return (T)MsgPackHelper.Deserialise(response.Body, typeof(T));
            }
            return JsonHelper.Deserialize<T>(responseText);
        }

        private void LogResponse(AblyResponse response)
        {
            if (Logger.IsDebug)
            {
                Logger.Debug("Protocol:" + _protocol);
                try
                {
                    var responseBody = response.TextResponse;
                    if (_protocol == Protocol.MsgPack && response.Body != null)
                    {
                        responseBody = MsgPackHelper.Deserialise(response.Body, typeof (MessagePackObject)).ToString();
                    }
                    Logger.Debug("Response: " + responseBody);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error while logging response body.", ex);
                }
            }
        }

        private IEnumerable<Stats> ParseStatsResponse(AblyResponse response)
        {
            var body = response.TextResponse;
            if (_protocol == Protocol.MsgPack)
            {
                return (List<Stats>)MsgPackHelper.Deserialise(response.Body, typeof(List<Stats>));
            }
            return JsonHelper.Deserialize<List<Stats>>(body);
        }

        private static int GetLimit(AblyRequest request)
        {
            if (request.QueryParameters.ContainsKey("limit"))
            {
                var limitQuery = request.QueryParameters["limit"];
                if (limitQuery.IsNotEmpty())
                    return int.Parse(limitQuery);
            }
            return Defaults.QueryLimit;
        }

        public ProtocolMessage ParseRealtimeData(RealtimeTransportData data)
        {
            ProtocolMessage protocolMessage;
            if (_protocol == Protocol.MsgPack)
            {
                protocolMessage = (ProtocolMessage)MsgPackHelper.Deserialise(data.Data, typeof(ProtocolMessage));
            }
            else
            {
                protocolMessage = JsonHelper.Deserialize<ProtocolMessage>(data.Text);
            }

            //Populate presence and message object timestamps
            if (protocolMessage != null)
            {
                foreach (var presenceMessage in protocolMessage.Presence)
                    presenceMessage.Timestamp = protocolMessage.Timestamp;

                foreach (var message in protocolMessage.Messages)
                    message.Timestamp = protocolMessage.Timestamp;

            }

            return protocolMessage;
        }

        public Result EncodeProtocolMessage(ProtocolMessage protocolMessage, ChannelOptions channelOptions)
        {
            var options = channelOptions ?? new ChannelOptions();
            var result = Result.Ok();
            foreach (var message in protocolMessage.Messages)
            {
                result = Result.Combine(result, EncodePayload(message, options));
            }

            foreach (var presence in protocolMessage.Presence)
            {
                result = Result.Combine(result, EncodePayload(presence, options));
            }
            return result;
        }

        public Result DecodeProtocolMessage(ProtocolMessage protocolMessage, ChannelOptions channelOptions)
        {
            var options = channelOptions ?? new ChannelOptions();

            return Result.Combine(
                DecodeMessages(protocolMessage, protocolMessage.Messages, options),
                DecodeMessages(protocolMessage, protocolMessage.Presence, options)
            );
        }

        private Result DecodeMessages(ProtocolMessage protocolMessage, IEnumerable<IMessage> messages, ChannelOptions options)
        {
            var result = Result.Ok();
            var index = 0;
            foreach(var message in messages ?? Enumerable.Empty<IMessage>())
            {
                SetMessageIdConnectionIdAndTimestamp(protocolMessage, message, index);
                result = Result.Combine(result, DecodePayload(message, options));
                index++;
            }
            return result;
        }

        private static void SetMessageIdConnectionIdAndTimestamp(ProtocolMessage protocolMessage, IMessage message, int i)
        {
            if (message.Id.IsEmpty())
                message.Id = $"{protocolMessage.Id}:{i}";
            if (message.ConnectionId.IsEmpty())
                message.ConnectionId = protocolMessage.ConnectionId;
            if (message.Timestamp.HasValue == false)
                message.Timestamp = protocolMessage.Timestamp;
        }

        public RealtimeTransportData GetTransportData(ProtocolMessage protocolMessage)
        {
            RealtimeTransportData data;
            if (_protocol == Protocol.MsgPack)
            {
                var bytes= MsgPackHelper.Serialise(protocolMessage);
                data = new RealtimeTransportData(bytes) { Original = protocolMessage };
            }
            else
            {
                var text = JsonHelper.Serialize(protocolMessage);
                data = new RealtimeTransportData(text) { Original = protocolMessage };
            }

            return data;
        }
    }
}
