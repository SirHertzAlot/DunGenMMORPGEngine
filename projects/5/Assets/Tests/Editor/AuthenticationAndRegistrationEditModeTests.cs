using NUnit.Framework;
using UnityEngine;
using DunGen.Networking;
using DunGen.Startup;

namespace DunGen.Tests
{
    [TestFixture]
    public class AuthenticationAndRegistrationEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            ClientAuthState.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ClientAuthState.Clear();
        }

        [Test]
        public void ClientAuthState_DefaultState_IsNotAuthenticated()
        {
            Assert.IsFalse(ClientAuthState.IsAuthenticated);
            Assert.IsFalse(ClientAuthState.HasValidToken);
            Assert.IsEmpty(ClientAuthState.AuthenticatedUsername);
            Assert.IsEmpty(ClientAuthState.AuthToken);
        }

        [Test]
        public void ClientAuthState_SetAuthenticatedSession_ValidatesProperly()
        {
            var expires = System.DateTime.UtcNow.AddHours(2);
            bool success = ClientAuthState.SetAuthenticatedSession("hero_player", "token_12345", "canary_abc", expires);

            Assert.IsTrue(success);
            Assert.IsTrue(ClientAuthState.IsAuthenticated);
            Assert.IsTrue(ClientAuthState.HasValidToken);
            Assert.AreEqual("hero_player", ClientAuthState.AuthenticatedUsername);
            Assert.AreEqual("token_12345", ClientAuthState.AuthToken);
            Assert.AreEqual("canary_abc", ClientAuthState.RequestCanary);
        }

        [Test]
        public void ClientAuthState_ExpiredToken_HasValidTokenReturnsFalse()
        {
            var expired = System.DateTime.UtcNow.AddHours(-1);
            ClientAuthState.SetAuthenticatedSession("hero_player", "token_12345", "canary_abc", expired);

            Assert.IsTrue(ClientAuthState.IsAuthenticated);
            Assert.IsFalse(ClientAuthState.HasValidToken);
        }

        [Test]
        public void BackendConnectionConfig_BuildsAllAuthEndpointsCorrectly()
        {
            var config = ScriptableObject.CreateInstance<BackendConnectionConfig>();

            var loginUrl = config.BuildAuthLoginUrl();
            var registerUrl = config.BuildAuthRegisterUrl();
            var forgotUrl = config.BuildAuthForgotUsernameUrl();
            var resetUrl = config.BuildAuthResetPasswordUrl();

            Assert.IsTrue(loginUrl.EndsWith("/v1/auth/login"));
            Assert.IsTrue(registerUrl.EndsWith("/v1/auth/register"));
            Assert.IsTrue(forgotUrl.EndsWith("/v1/auth/forgot-username"));
            Assert.IsTrue(resetUrl.EndsWith("/v1/auth/reset-password"));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AuthDtos_JsonRoundtripSerialization()
        {
            var regReq = new UnityAuthRegisterRequestDto
            {
                username = "adventurer",
                email = "adventurer@realm.net",
                password = "mypassword123"
            };
            var regJson = JsonUtility.ToJson(regReq);
            var deserializedReg = JsonUtility.FromJson<UnityAuthRegisterRequestDto>(regJson);
            Assert.AreEqual(regReq.username, deserializedReg.username);
            Assert.AreEqual(regReq.email, deserializedReg.email);
            Assert.AreEqual(regReq.password, deserializedReg.password);

            var forgotReq = new UnityAuthForgotUsernameRequestDto { email = "lost@realm.net" };
            var forgotJson = JsonUtility.ToJson(forgotReq);
            var deserializedForgot = JsonUtility.FromJson<UnityAuthForgotUsernameRequestDto>(forgotJson);
            Assert.AreEqual(forgotReq.email, deserializedForgot.email);

            var resetReq = new UnityAuthResetPasswordRequestDto { usernameOrEmail = "lost@realm.net", newPassword = "newsecretpass" };
            var resetJson = JsonUtility.ToJson(resetReq);
            var deserializedReset = JsonUtility.FromJson<UnityAuthResetPasswordRequestDto>(resetJson);
            Assert.AreEqual(resetReq.usernameOrEmail, deserializedReset.usernameOrEmail);
            Assert.AreEqual(resetReq.newPassword, deserializedReset.newPassword);
        }
    }
}
