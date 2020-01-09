#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;
using Grpc.AspNetCore.FunctionalTests.Infrastructure;
using Grpc.Gateway.Testing;
using Grpc.Net.Client.Web;
using Grpc.Tests.Shared;
using NUnit.Framework;

namespace Grpc.AspNetCore.FunctionalTests.Web.Server
{
    [TestFixture(GrpcTestMode.GrpcWeb, TestServerEndpointName.Http1)]
    [TestFixture(GrpcTestMode.GrpcWeb, TestServerEndpointName.Http2)]
    [TestFixture(GrpcTestMode.GrpcWebText, TestServerEndpointName.Http1)]
    [TestFixture(GrpcTestMode.GrpcWebText, TestServerEndpointName.Http2)]
    [TestFixture(GrpcTestMode.Grpc, TestServerEndpointName.Http2)]
    public class UnaryMethodTests : GrpcWebFunctionalTestBase
    {
        public UnaryMethodTests(GrpcTestMode grpcTestMode, TestServerEndpointName endpointName)
         : base(grpcTestMode, endpointName)
        {
        }

        [Test]
        public async Task SendValidRequest_SuccessResponse()
        {
            // Arrange
            var requestMessage = new EchoRequest
            {
                Message = "test"
            };

            var ms = new MemoryStream();
            MessageHelpers.WriteMessage(ms, requestMessage);

            // Act
            var grpcWebClient = CreateGrpcWebClient();
            var response = await grpcWebClient.PostAsync(
                "grpc.gateway.testing.EchoService/Echo",
                new GrpcStreamContent(ms)).DefaultTimeout();

            // Assert
            response.AssertIsSuccessfulGrpcRequest();

            var s = await response.Content.ReadAsStreamAsync();
            var reader = PipeReader.Create(s);

            var message = await MessageHelpers.AssertReadStreamMessageAsync<EchoResponse>(reader);
            Assert.AreEqual("test", message!.Message);

            response.AssertTrailerStatus();
        }

        [Test]
        public async Task SendValidRequest_ServerAbort_AbortResponse()
        {
            SetExpectedErrorsFilter(writeContext =>
            {
                if (writeContext.LoggerName == TestConstants.ServerCallHandlerTestName &&
                    writeContext.EventId.Name == "RpcConnectionError" &&
                    writeContext.State.ToString() == "Error status code 'Aborted' raised.")
                {
                    return true;
                }

                return false;
            });

            // Arrange
            var requestMessage = new EchoRequest
            {
                Message = "test"
            };

            var ms = new MemoryStream();
            MessageHelpers.WriteMessage(ms, requestMessage);

            // Act
            var response = await Fixture.Client.PostAsync(
                "grpc.gateway.testing.EchoService/EchoAbort",
                new GrpcStreamContent(ms)).DefaultTimeout();

            // Assert
            response.AssertIsSuccessfulGrpcRequest();

            var s = await response.Content.ReadAsStreamAsync();
            var reader = PipeReader.Create(s);

            var readResult = await reader.ReadAsync();
            Assert.AreEqual(0, readResult.Buffer.Length);
            Assert.IsTrue(readResult.IsCompleted);

            Assert.AreEqual("10", response.Headers.GetValues("grpc-status").Single());
            Assert.AreEqual("Aborted from server side.", response.Headers.GetValues("grpc-message").Single());
        }
    }
}
