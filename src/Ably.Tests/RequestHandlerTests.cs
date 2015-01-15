﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Xunit;

namespace Ably.Tests
{
    public class RequestHandlerTests
    {
        [Fact]
        public void GetRequestBody_WithSingleMessage_ReturnsByteArrayOfMessagePayloadObject()
        {
            //Arrange
            var message = new Message() {Name = "Martin", Data = "Test"};
            var handler = new RequestHandler();
            var request = GetRequest(message);

            //Act
            var result = handler.GetRequestBody(request);
            var payload = GetPayload<MessagePayload>(result);
            
            //Assert
            Assert.NotNull(payload);
        }

        [Fact]
        public void GetRequestBody_WithMultipleMessages_ReturnsArrayOfMessagePayloads()
        {
            //Arrange
            var messages = new [] { new Message() { Name = "Martin", Data = "Test" }, new Message() { Name = "Martin", Data = "Test" }};
            var handler = new RequestHandler();
            var request = GetRequest(messages);

            //Act
            var result = handler.GetRequestBody(request);
            var payload = GetPayload<List<MessagePayload>>(result);

            //Assert
            Assert.NotNull(payload);
            Assert.Equal(2, payload.Count);
        }

        [Fact]
        public void GetRequestBody_WhenPostDataIsNull_ReturnsEmptyByteArray()
        {
            //Arrange
            var request = GetRequest(null);

            //Act
            var result = new RequestHandler().GetRequestBody(request);

            //Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetRequestBody_WhenUseTextProtocolIsFalse_ThrowsException()
        {
            //Arrange
            var request = GetRequest(new object(), useTextProtocol: false);

            //Act
            var ex = Assert.Throws<AblyException>(delegate
            {
                new RequestHandler().GetRequestBody(request);
            });

            //Assert
            Assert.Equal(HttpStatusCode.InternalServerError, ex.ErrorInfo.StatusCode);
            Assert.Equal(50000, ex.ErrorInfo.Code);
        }

        [Fact]
        public void GetRequestBody_WithObjectThatIsNotAMessage_ReturnsObjectInPayloadData()
        {
            //Arrange
            var handler = new RequestHandler();
            var date = new DateTime(2014, 1, 1);
            var request = GetRequest(date);

            //Act
            var result = handler.GetRequestBody(request);
            var payload = GetPayload<DateTime>(result);

            //Assert
            Assert.Equal(date, payload);
        }

        [Fact]
        public void GetMessagePayload_WithSingleMessageAndEcryptionOff_ReturnsPayloadWithCorrectNameTimestampAndData()
        {
            var message = new Message() { Name = "Martin", Data = "Test" };

            //Act
            var result = RequestHandler.CreateMessagePayload(message, false, null);

            //Assert
            Assert.Equal(message.Data, result.Data);
            Assert.Equal(message.Name, result.Name);
            Assert.Equal(message.TimeStamp.ToUnixTime(), result.Timestamp);
        }

        [Fact]
        public void GetMessagePayload_WithMessageWithBinaryData_ReturnPayloadWithBase64EncodedBinaryData()
        {
            var message = new Message() { Name = "Martin", Data = new byte[] { 1, 2, 3, 4, 5}};

            //Act
            var result = RequestHandler.CreateMessagePayload(message, false, null);

            //Assert
            Assert.Equal(message.Value<byte[]>().ToBase64(), result.Data);
            Assert.Equal(message.Name, result.Name);
            Assert.Equal("base64", result.Encoding);
            Assert.Equal(message.TimeStamp.ToUnixTime(), result.Timestamp);
        }

        [Fact]
        public void GetMessagePayload_WithMessageDataAndEncryptionOn_ReturnPayloadWithEncryptedPayload()
        {
            //Arrange
            var message = new Message() {Name = "Martin", Data = "EncryptionTest"};
            var plainTextData = "";
            var cipherParams = Crypto.GetDefaultParams();

            //Act
            var result = RequestHandler.CreateMessagePayload(message, true, cipherParams); //Uses default params for encryption

            //Assert
            var cipher = Config.GetCipher(cipherParams);
            //Assert.Equal(plainTextData.Type.ToString(), result.Type);
            Assert.Equal(message.Name, result.Name);
            var decryptedValue = cipher.Decrypt(result.Data.ToString().FromBase64()).GetText();
            Assert.Equal(message.Data, decryptedValue);
            Assert.Equal("cipher+base64", result.Encoding);
        }

        private T GetPayload<T>(byte[] bodyBytes)
        {
            return JsonConvert.DeserializeObject<T>(bodyBytes.GetText());
        }

        private AblyRequest GetRequest(object data, bool encrypted = false, CipherParams cipherParams = null, bool useTextProtocol = true)
        {
            var request = new AblyRequest("", HttpMethod.Get);
            request.UseTextProtocol = useTextProtocol;
            request.PostData = data;
            request.Encrypted = encrypted;
            request.CipherParams = cipherParams;
            return request;
        }
    }
}